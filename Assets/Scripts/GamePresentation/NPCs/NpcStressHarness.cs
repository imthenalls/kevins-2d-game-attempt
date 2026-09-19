using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Runtime performance harness for NPC pathfinding. Spawns 25 / 50 / 100 generic wandering NPCs in
/// the NPC Stress Room and measures, for each count: a baseline (frozen), steady wandering (each NPC
/// re-paths on its own), and a synchronized burst (every NPC re-paths in one frame).
///
/// Unity setup: none — created by Tools &gt; Stress &gt; Run NPC Pathfinding Stress, which sets the
/// spawn bounds. Writes Temp/npc-stress-report.txt and logs a summary, then tears everything down.
/// Do not interact with the game while it runs.
/// </summary>
public sealed class NpcStressHarness : MonoBehaviour
{
    [SerializeField] private int spawnCellMinX = -1;
    [SerializeField] private int spawnCellMinY = -1;
    [SerializeField] private int spawnCellMaxX = 1;
    [SerializeField] private int spawnCellMaxY = 1;
    [SerializeField] private float npcScale = 0.5f;
    [SerializeField] private float phaseSeconds = 6f;
    [SerializeField] private float burstInterval = 2f;
    [SerializeField] private float burstWindowSeconds = 6f;
    [SerializeField] private int[] npcCounts = { 25, 50, 100 };
    [SerializeField] private string reportPath = "Temp/npc-stress-report.txt";

    private readonly List<GameObject> spawned = new();
    private readonly List<NpcWanderBehavior> wanderers = new();
    private readonly List<NpcBehaviorManager> managers = new();
    private readonly StringBuilder report = new();
    private Camera stressCamera;
    private Camera previousCamera;

    private void Start()
    {
        StartCoroutine(RunAll());
    }

    private IEnumerator RunAll()
    {
        report.AppendLine("NPC pathfinding stress report");
        report.AppendLine("scale=" + npcScale + " phase=" + phaseSeconds + "s burstEvery=" + burstInterval + "s");
        report.AppendLine();

        SetUpCamera();

        // Two obstacle-mask configurations: the current game default (Everything, so NPC bodies
        // block pathfinding) and the walls-only config a layer fix would give.
        yield return RunMask("obstacles=Everything (NPCs block)", obstacleMask: ~0);
        yield return RunMask("obstacles=NoNpcBodies", obstacleMask: null);

        TearDownCamera();
        WriteReport();
        Debug.Log("[NPC Stress] done. See " + reportPath);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator RunMask(string label, int? obstacleMask)
    {
        report.AppendLine("-- " + label + " --");
        Debug.Log("[NPC Stress] " + label);

        foreach (int count in npcCounts)
        {
            Spawn(count, obstacleMask);
            yield return null;

            yield return Measure(label + " baseline", count, freeze: true, burst: false);
            yield return Measure(label + " steady", count, freeze: false, burst: false);
            yield return Measure(label + " burst", count, freeze: false, burst: true);

            Despawn();
            yield return null;
        }
    }

    private IEnumerator Measure(string label, int count, bool freeze, bool burst)
    {
        float duration = burst ? burstWindowSeconds : phaseSeconds;
        float nextBurst = burst ? burstInterval : float.MaxValue;
        float burstElapsed = 0f;

        int frames = 0;
        double sumMs = 0d;
        float minMs = float.MaxValue;
        float maxMs = 0f;
        double sumBurstMs = 0d;
        int bursts = 0;
        float worstBurstMs = 0f;
        long sumPathsOk = 0;
        long sumPathsTotal = 0;

        SetFreeze(freeze);

        float end = Time.realtimeSinceStartup + duration;
        while (Time.realtimeSinceStartup < end)
        {
            float dt = Time.unscaledDeltaTime;
            frames++;
            sumMs += dt * 1000d;
            float ms = dt * 1000f;
            if (ms < minMs) minMs = ms;
            if (ms > maxMs) maxMs = ms;

            if (burst)
            {
                burstElapsed += dt;
                if (burstElapsed >= nextBurst)
                {
                    burstElapsed = 0f;
                    float burstMs = RepathAll(out int ok, out int total);
                    sumBurstMs += burstMs;
                    sumPathsOk += ok;
                    sumPathsTotal += total;
                    bursts++;
                    if (burstMs > worstBurstMs) worstBurstMs = burstMs;
                }
            }

            yield return null;
        }

        double avgMs = frames > 0 ? sumMs / frames : 0d;
        float avgFps = avgMs > 0d ? (float)(1000d / avgMs) : 0f;
        string line = $"{count,4} NPCs  {label,-26}  fps~{avgFps,6:0.0}  avg {avgMs,7:0.00}ms  min {minMs,7:0.00}  max {maxMs,7:0.00}";
        if (burst)
            line += $"   | repath-all {avgBurstMs(sumBurstMs, bursts),7:0.00}ms avg, worst {worstBurstMs,7:0.00}ms, paths {avgBurstMs(sumPathsOk, bursts),5:0.0}/{avgBurstMs(sumPathsTotal, bursts),5:0.0}";
        report.AppendLine(line);
        Debug.Log("[NPC Stress] " + line);
    }

    private static double avgBurstMs(double sum, int count) => count > 0 ? sum / count : 0d;

    private float RepathAll(out int pathsOk, out int pathsTotal)
    {
        pathsOk = 0;
        pathsTotal = wanderers.Count;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < wanderers.Count; i++)
        {
            NpcWanderBehavior wanderer = wanderers[i];
            if (wanderer == null)
                continue;

            wanderer.Repath();
            if (wanderer.HasPath)
                pathsOk++;
        }
        sw.Stop();
        return (float)sw.Elapsed.TotalMilliseconds;
    }

    private void SetFreeze(bool freeze)
    {
        foreach (NpcBehaviorManager manager in managers)
        {
            if (manager != null)
                manager.enabled = !freeze;
        }

        if (freeze)
        {
            foreach (GameObject go in spawned)
            {
                if (go != null && go.TryGetComponent(out Rigidbody2D body))
                    body.linearVelocity = Vector2.zero;
            }
        }
    }

    private void Spawn(int count, int? obstacleMask)
    {
        Grid grid = Object.FindAnyObjectByType<Grid>();
        Sprite sprite = CreateSprite();

        int npcLayer = LayerMask.NameToLayer("Npc");
        if (npcLayer < 0) npcLayer = 0;
        int pathMask = obstacleMask ?? (npcLayer >= 0 ? ~(1 << npcLayer) : ~0);

        int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        int spanX = Mathf.Max(1, spawnCellMaxX - spawnCellMinX);
        int spanY = Mathf.Max(1, spawnCellMaxY - spawnCellMinY);

        for (int i = 0; i < count; i++)
        {
            int cellX = spawnCellMinX + Mathf.RoundToInt((i % columns + 0.5f) * spanX / columns);
            int cellY = spawnCellMinY + Mathf.RoundToInt((i / columns + 0.5f) * spanY / rows);
            Vector3 position = grid.GetCellCenterWorld(new Vector3Int(cellX, cellY, 0));

            GameObject go = CreateNpc(i, position, sprite, npcLayer, pathMask);
            spawned.Add(go);
            var wanderer = go.GetComponent<NpcWanderBehavior>();
            if (wanderer != null)
                wanderers.Add(wanderer);
            var manager = go.GetComponent<NpcBehaviorManager>();
            if (manager != null)
                managers.Add(manager);
        }
    }

    private GameObject CreateNpc(int index, Vector3 position, Sprite sprite, int npcLayer, int pathMask)
    {
        var go = new GameObject("StressNPC_" + index);
        go.SetActive(false); // configure before Awake so behaviours are discovered
        go.layer = npcLayer;
        go.transform.position = position;
        go.transform.localScale = Vector3.one * npcScale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.HSVToRGB((index % 20) / 20f, 0.7f, 0.9f);

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.4f, 0.4f);

        var controller = go.AddComponent<NpcController>();
        SetPrivateField(controller, "npcId", "stress_" + index);

        go.AddComponent<NpcBehaviorManager>();
        var pathfinder = go.AddComponent<NpcPathfinder>();
        SetPrivateLayerMask(pathfinder, "obstacleLayers", pathMask);
        go.AddComponent<NpcPerception>();
        var wanderer = go.AddComponent<NpcWanderBehavior>();
        SetPrivateLayerMask(wanderer, "wallLayers", pathMask);

        go.SetActive(true);
        return go;
    }

    private static void SetPrivateLayerMask(object target, string name, int mask)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(LayerMask))
            field.SetValue(target, (LayerMask)mask);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
            field.SetValue(target, value);
    }

    private static Sprite cachedSprite;

    private static Sprite CreateSprite()
    {
        if (cachedSprite != null)
            return cachedSprite;

        Texture2D texture = Texture2D.whiteTexture;
        cachedSprite = Sprite.Create(
            texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1f);
        return cachedSprite;
    }

    private void SetUpCamera()
    {
        previousCamera = Camera.main;
        if (previousCamera != null)
            previousCamera.enabled = false;

        Grid grid = Object.FindAnyObjectByType<Grid>();
        Vector3 a = grid.GetCellCenterWorld(new Vector3Int(spawnCellMinX, spawnCellMinY, 0));
        Vector3 b = grid.GetCellCenterWorld(new Vector3Int(spawnCellMaxX, spawnCellMinY, 0));
        Vector3 c = grid.GetCellCenterWorld(new Vector3Int(spawnCellMinX, spawnCellMaxY, 0));
        Vector3 d = grid.GetCellCenterWorld(new Vector3Int(spawnCellMaxX, spawnCellMaxY, 0));
        float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
        float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
        float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));

        var go = new GameObject("NPC Stress Camera", typeof(Camera));
        stressCamera = go.GetComponent<Camera>();
        stressCamera.orthographic = true;
        stressCamera.orthographicSize = (maxY - minY) * 0.5f + 2f;
        stressCamera.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
        stressCamera.clearFlags = CameraClearFlags.SolidColor;
        stressCamera.transform.position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, -10f);
        go.tag = "MainCamera";
    }

    private void TearDownCamera()
    {
        if (stressCamera != null)
            Destroy(stressCamera.gameObject);
        if (previousCamera != null)
            previousCamera.enabled = true;
    }

    private void Despawn()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
                Destroy(go);
        }
        spawned.Clear();
        wanderers.Clear();
        managers.Clear();
    }

    private void WriteReport()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? "Temp");
        File.WriteAllText(reportPath, report.ToString());
    }

    private void OnDestroy()
    {
        TearDownCamera();
    }
}
