using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the underground hallway as a separate area inside the Town scene: a long inverted-U
/// corridor with two bends, its two ends near the bottom. The school-side end holds the
/// <c>hall_entrance</c> portal (a same-scene round trip to the Industrial Arts Shop); the far end
/// holds the one-way <c>hall_exit</c> portal that drops the player into the town park at the
/// arrival-only <c>park_arrival</c> destination. The park has no return trigger.
///
/// Gameplay stays on the XZ plane with the existing Town player and camera. The corridor is floored,
/// enclosed by height-1 collision walls on the Walls layer, and fully visible to the camera. Each
/// portal sits on a light-green pad with a red centre.
///
/// Unity setup: none directly — Tools &gt; Worlds &gt; Rebuild Town Scene (3D) calls
/// <see cref="BuildInto"/>, and Tools &gt; Worlds &gt; Town 3D &gt; Refresh Underground Hallway
/// rebuilds it inside the open Town scene.
///
/// Runtime API: none (editor only).
/// </summary>
public static class UndergroundHallwayBuilder
{
    public const string RootName = "Underground Hallway";
    public const string HallEntrancePortalId = "hall_entrance";
    public const string HallExitPortalId = "hall_exit";
    public const string ParkArrivalId = "park_arrival";

    private const string MaterialsDir = "Assets/Materials/Town3D";
    private const string SpritePath =
        "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png";
    private const float WallHeight = 1f;
    private const float WallThickness = 0.3f;

    private readonly struct CellRect
    {
        public readonly int X, Z, W, D;
        public CellRect(int x, int z, int w, int d) { X = x; Z = z; W = w; D = d; }
    }

    // Left leg, top bar, right leg — union forms the inverted U. Ends sit at low Z (near the bottom).
    private static readonly CellRect[] HallwayRects =
    {
        new CellRect(12, -90, 3, 22),
        new CellRect(12, -69, 19, 3),
        new CellRect(28, -90, 3, 22),
    };

    private static Sprite squareSprite;

    [MenuItem("Tools/Worlds/Town 3D/Refresh Underground Hallway", priority = 24)]
    public static void RefreshInOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode first.");

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != Town3DSceneBuilder.ScenePath)
            throw new System.InvalidOperationException("Open " + Town3DSceneBuilder.ScenePath + " before refreshing the hallway.");

        Transform existing = GameObject.Find(RootName)?.transform;
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        BuildInto();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[UndergroundHallway] Hallway refreshed (hall_entrance + hall_exit + park_arrival).");
    }

    /// <summary>Builds the hallway at the origin (cells are already world-space XZ coordinates).</summary>
    public static GameObject BuildInto()
    {
        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);

        var root = new GameObject(RootName);
        var cells = BuildCells();

        int wallsLayer = Mathf.Max(0, LayerMask.NameToLayer("Walls"));
        Material floor = EnsureMaterial("HallwayFloor", new Color(0.24f, 0.23f, 0.26f), unlit: true);
        Material wall = EnsureMaterial("HallwayWall", new Color(0.16f, 0.15f, 0.18f), unlit: false);

        BuildGround(root.transform);
        CreateCellMesh("Underground Hallway Floor", cells, 0.03f, floor, root.transform);
        BuildWalls(root.transform, cells, wallsLayer, wall);

        BuildHallEntrance(root.transform);
        BuildHallExit(root.transform);
        BuildParkArrival();

        return root;
    }

    private static HashSet<Vector2Int> BuildCells()
    {
        var cells = new HashSet<Vector2Int>();
        foreach (CellRect r in HallwayRects)
            for (int x = r.X; x < r.X + r.W; x++)
                for (int z = r.Z; z < r.Z + r.D; z++)
                    cells.Add(new Vector2Int(x, z));
        return cells;
    }

    private static void BuildGround(Transform parent)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.position = new Vector3(21f, -0.08f, -79f);
        ground.transform.localScale = new Vector3(2.8f, 1f, 2.8f);
        ground.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial("HallwayGround", new Color(0.08f, 0.08f, 0.10f), unlit: true);
    }

    // Encloses the corridor cell set with height-1 wall boxes, merging collinear edges.
    private static void BuildWalls(Transform parent, HashSet<Vector2Int> cells, int layer, Material material)
    {
        var north = new Dictionary<int, List<int>>();
        var south = new Dictionary<int, List<int>>();
        var east = new Dictionary<int, List<int>>();
        var west = new Dictionary<int, List<int>>();

        foreach (Vector2Int c in cells)
        {
            if (!cells.Contains(new Vector2Int(c.x, c.y + 1))) AddEdge(north, c.y + 1, c.x);
            if (!cells.Contains(new Vector2Int(c.x, c.y - 1))) AddEdge(south, c.y, c.x);
            if (!cells.Contains(new Vector2Int(c.x + 1, c.y))) AddEdge(east, c.x + 1, c.y);
            if (!cells.Contains(new Vector2Int(c.x - 1, c.y))) AddEdge(west, c.x, c.y);
        }

        AddRuns(parent, layer, material, north, horizontal: true);
        AddRuns(parent, layer, material, south, horizontal: true);
        AddRuns(parent, layer, material, east, horizontal: false);
        AddRuns(parent, layer, material, west, horizontal: false);
    }

    private static void AddRuns(
        Transform parent, int layer, Material material, Dictionary<int, List<int>> edges, bool horizontal)
    {
        foreach (KeyValuePair<int, List<int>> kv in edges)
        {
            foreach ((int a, int b) run in MergeRuns(kv.Value))
            {
                Vector3 position = horizontal
                    ? new Vector3((run.a + run.b + 1) * 0.5f, WallHeight * 0.5f, kv.Key)
                    : new Vector3(kv.Key, WallHeight * 0.5f, (run.a + run.b + 1) * 0.5f);
                Vector3 scale = horizontal
                    ? new Vector3(run.b - run.a + 1, WallHeight, WallThickness)
                    : new Vector3(WallThickness, WallHeight, run.b - run.a + 1);
                AddWall(parent, layer, material, position, scale);
            }
        }
    }

    private static void AddWall(Transform parent, int layer, Material material, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.layer = layer;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // Same-scene school-end portal. Returning to the workshop is allowed; its exit point is north of
    // the trigger so an arriving traveler never bounces straight back.
    private static void BuildHallEntrance(Transform parent)
    {
        var portalObject = CreatePortalBase("Hall Entrance Portal", parent, 13.5f, -89.5f);
        var exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(portalObject.transform, false);
        exitPoint.localPosition = new Vector3(0f, 0f, 2f);

        var portal = portalObject.AddComponent<PortalTrigger3D>();
        SetStringField(portal, "portalId", HallEntrancePortalId);
        SetStringField(portal, "destinationPortalId", SchoolInteriorBuilder.WorkshopPortalId);
        SetObjectField(portal, "exitPoint", exitPoint);

        SchoolInteriorBuilder.BuildPortalPad(portalObject.transform, "HallEntrancePad");
    }

    // One-way far-end portal; the park destination has no return route.
    private static void BuildHallExit(Transform parent)
    {
        var portalObject = CreatePortalBase("Hall Exit Portal", parent, 29.5f, -89.5f);
        var exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(portalObject.transform, false);
        exitPoint.localPosition = new Vector3(0f, 0f, 2f);

        var portal = portalObject.AddComponent<PortalTrigger3D>();
        SetStringField(portal, "portalId", HallExitPortalId);
        SetStringField(portal, "destinationPortalId", ParkArrivalId);
        SetObjectField(portal, "exitPoint", exitPoint);

        SchoolInteriorBuilder.BuildPortalPad(portalObject.transform, "HallExitPad");
    }

    private static GameObject CreatePortalBase(string name, Transform parent, float x, float z)
    {
        var portalObject = new GameObject(name);
        portalObject.transform.SetParent(parent, false);
        portalObject.transform.position = new Vector3(x, 0f, z);

        var collider = portalObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1.6f, 2f, 1.6f);
        collider.center = new Vector3(0f, 1f, 0f);

        var visual = new GameObject("PortalVisual");
        visual.transform.SetParent(portalObject.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite != null ? squareSprite : LoadSquareSprite();
        renderer.color = new Color(0.9f, 0.22f, 0.21f);
        renderer.sortingOrder = 50;
        visual.AddComponent<BillboardSprite>();
        return portalObject;
    }

    // The park destination: an arrival point with no collider, no trigger, and no outgoing route.
    private static void BuildParkArrival()
    {
        var arrivalObject = new GameObject("Park Arrival");
        arrivalObject.transform.position = new Vector3(45.5f, 0f, 33.5f);

        var arrival = arrivalObject.AddComponent<PortalArrival3D>();
        SetStringField(arrival, "portalId", ParkArrivalId);
    }

    private static Sprite LoadSquareSprite()
    {
        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        return squareSprite;
    }

    private static void AddEdge(Dictionary<int, List<int>> map, int key, int value)
    {
        if (!map.TryGetValue(key, out List<int> list))
        {
            list = new List<int>();
            map[key] = list;
        }
        list.Add(value);
    }

    private static List<(int a, int b)> MergeRuns(List<int> values)
    {
        values.Sort();
        var runs = new List<(int a, int b)>();
        int i = 0;
        while (i < values.Count)
        {
            int start = values[i];
            int end = start;
            while (i + 1 < values.Count && values[i + 1] == end + 1)
            {
                i++;
                end = values[i];
            }
            runs.Add((start, end));
            i++;
        }
        return runs;
    }

    private static void CreateCellMesh(
        string name, HashSet<Vector2Int> cells, float height, Material material, Transform parent)
    {
        var vertices = new List<Vector3>(cells.Count * 4);
        var triangles = new List<int>(cells.Count * 6);

        foreach (Vector2Int cell in cells)
        {
            float x = cell.x;
            float z = cell.y;
            int b = vertices.Count;
            vertices.Add(new Vector3(x, height, z));
            vertices.Add(new Vector3(x + 1f, height, z));
            vertices.Add(new Vector3(x + 1f, height, z + 1f));
            vertices.Add(new Vector3(x, height, z + 1f));
            triangles.Add(b + 0); triangles.Add(b + 3); triangles.Add(b + 2);
            triangles.Add(b + 0); triangles.Add(b + 2); triangles.Add(b + 1);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        Directory.CreateDirectory(MaterialsDir);
        string meshPath = MaterialsDir + "/" + name + "Mesh.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
            AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
    }

    private static Material EnsureMaterial(string name, Color color, bool unlit)
    {
        Directory.CreateDirectory(MaterialsDir);
        string path = MaterialsDir + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (existing != null && existing.shader == shader)
        {
            ApplyMaterial(existing, color, unlit);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var material = new Material(shader) { name = name };
        ApplyMaterial(material, color, unlit);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void ApplyMaterial(Material material, Color color, bool unlit)
    {
        material.SetColor("_BaseColor", color);
        if (!unlit)
        {
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_Metallic", 0f);
        }
    }

    private static void SetStringField(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetObjectField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
