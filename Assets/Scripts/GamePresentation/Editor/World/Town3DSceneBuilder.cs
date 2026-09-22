using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds <c>Assets/Scenes/Town.unity</c> as a 3D planar-isometric scene: real geometry on the XZ
/// plane, a 3D isometric camera and light, and billboarded sprites for the player. Replaces the old
/// painted-tilemap Town (see <see cref="TownSceneBuilder"/> / Documents/TOWN_SCENE.md).
///
/// The scene asset path (and therefore its reference in Build Settings and portals) is unchanged.
/// Buildings keep their solid footprints as 3D <c>BoxCollider</c>s on the <c>Walls</c> layer, so the
/// player is blocked exactly where the old 2D polygon colliders were.
///
/// Doors, NPCs and portal routing are ported in the follow-up slice; this builder lays down the 3D
/// shell (ground, roads, buildings, walls, light, camera, spawn, player).
///
/// Unity setup: menu Tools &gt; Worlds &gt; Rebuild Town Scene (3D). Refuses in Play Mode.
/// </summary>
public static class Town3DSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";
    private const string MaterialsDir = "Assets/Materials/Town3D";
    private const string SpritePath =
        "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png";

    private const int GridW = 64;
    private const int GridH = 44;
    private const float CellSize = 1f;
    private const float BuildingHeight = 3f;
    private const float WallHeight = 1f;

    private static readonly List<(int x, int y, int w, int d, char side)> Buildings = new()
    {
        (4, 14, 4, 3, 'S'), (12, 14, 4, 3, 'S'), (20, 14, 4, 3, 'S'), (4, 4, 4, 3, 'S'),
        (35, 14, 4, 3, 'S'), (43, 14, 4, 3, 'S'), (51, 14, 4, 3, 'S'), (35, 4, 4, 3, 'S'),
        (4, 24, 4, 3, 'N'), (12, 24, 4, 3, 'N'), (4, 34, 4, 3, 'N'),
        (35, 24, 4, 3, 'N'), (53, 24, 4, 3, 'N'), (35, 36, 4, 3, 'N'),
    };

    private static Scene scene;
    private static Sprite squareSprite;

    [MenuItem("Tools/Worlds/Rebuild Town Scene (3D)")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode first.");

        Directory.CreateDirectory(MaterialsDir);
        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (squareSprite == null)
            throw new System.InvalidOperationException("Square sprite not found: " + SpritePath);

        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConfigurePlaceholderLighting();

        var identity = new GameObject("Town Scene Identity");
        identity.AddComponent<WorldSceneIdentity>();
        SetEnumField(identity.GetComponent<WorldSceneIdentity>(), "world", (int)WorldLayer.WorldA);
        identity.AddComponent<Isometric3DScene>();

        BuildGround();
        BuildRoads();
        BuildPark();
        BuildBuildings();
        BuildPerimeterWalls();
        BuildTownNpcs();
        BuildCamera();
        BuildSun();

        var spawn = new GameObject("Player Spawn");
        spawn.AddComponent<PlayerSpawnPoint>();
        spawn.transform.position = CellToWorld(10, 10);

        BuildPlayer(spawn.transform.position);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        AddToBuildSettings(ScenePath);

        // Snap the Scene view to the isometric angle so the rebuilt scene is framed immediately.
        EditorApplication.delayCall += Isometric3DSceneView.Focus;

        Directory.CreateDirectory("Temp");
        var report = new StringBuilder();
        report.AppendLine("Town 3D scene rebuilt: " + ScenePath);
        report.AppendLine("grid " + GridW + "x" + GridH + " cells @ " + CellSize + " world unit");
        report.AppendLine("buildings (3D boxes, Walls layer): " + Buildings.Count);
        report.AppendLine("ground/roads/park are meshes on the XZ plane; player is PlayerController3D");
        File.WriteAllText("Temp/town3d-build.txt", report.ToString());
        Debug.Log("[Town3D] " + report);
    }

    // ── World geometry ───────────────────────────────────────────────────────

    private static void BuildGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(GridW * 0.5f, 0f, GridH * 0.5f);
        ground.transform.localScale = new Vector3(GridW / 10f, 1f, GridH / 10f);
        ground.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial("Grass", new Color(0.30f, 0.69f, 0.31f), unlit: true);
    }

    private static void BuildRoads()
    {
        var cells = new HashSet<Vector2Int>();
        for (int cx = 30; cx <= 32; cx++)
            for (int cy = 0; cy < GridH; cy++) cells.Add(new Vector2Int(cx, cy));
        for (int cy = 20; cy <= 22; cy++)
            for (int cx = 0; cx < GridW; cx++) cells.Add(new Vector2Int(cx, cy));
        for (int cx = 0; cx < GridW; cx++)
            for (int cy = 0; cy < GridH; cy++)
                if (cx < 3 || cy < 3 || cx >= GridW - 3 || cy >= GridH - 3)
                    cells.Add(new Vector2Int(cx, cy));

        CreateCellMesh("Roads", cells, 0.02f, EnsureMaterial("Street", new Color(0.22f, 0.28f, 0.31f), unlit: true));
    }

    private static void BuildPark()
    {
        var grass = new HashSet<Vector2Int>();
        var plaza = new HashSet<Vector2Int>();
        for (int cx = 44; cx < 52; cx++)
        {
            for (int cy = 30; cy < 36; cy++)
            {
                bool isPlaza = cx >= 47 && cx < 50 && cy >= 32 && cy < 35;
                if (isPlaza) plaza.Add(new Vector2Int(cx, cy));
                else grass.Add(new Vector2Int(cx, cy));
            }
        }

        Color parkGreen = new Color(0.42f, 0.78f, 0.36f);
        CreateCellMesh("Park", grass, 0.03f, EnsureMaterial("Park", parkGreen, unlit: true));
        CreateCellMesh("Plaza", plaza, 0.04f, EnsureMaterial("Plaza", new Color(0.90f, 0.22f, 0.21f), unlit: true));
    }

    private static void BuildBuildings()
    {
        var root = new GameObject("Buildings");
        int wallsLayer = Mathf.Max(0, LayerMask.NameToLayer("Walls"));
        Material body = EnsureMaterial("Building", new Color(0.69f, 0.75f, 0.77f), unlit: false);

        foreach (var b in Buildings)
        {
            float centerX = b.x + b.w * 0.5f;
            float centerZ = b.y + b.d * 0.5f;

            GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
            building.name = "Building_" + b.x + "_" + b.y;
            building.layer = wallsLayer;
            building.transform.SetParent(root.transform, false);
            building.transform.position = new Vector3(centerX, BuildingHeight * 0.5f, centerZ);
            building.transform.localScale = new Vector3(b.w, BuildingHeight, b.d);
            building.GetComponent<MeshRenderer>().sharedMaterial = body;

            int doorCellX = b.x + b.w / 2;
            // 'S' buildings face the street on their +z edge; 'N' buildings on their -z edge.
            float doorX = doorCellX + 0.5f;
            float doorZ = b.side == 'S' ? b.y + b.d + 0.25f : b.y - 0.25f;

            var marker = new GameObject("Door");
            marker.transform.SetParent(root.transform, false);
            marker.transform.localScale = new Vector3(0.7f, 1.3f, 1f);
            marker.transform.position = new Vector3(doorX, 0.65f, doorZ);
            var doorRenderer = marker.AddComponent<SpriteRenderer>();
            doorRenderer.sprite = squareSprite;
            doorRenderer.color = new Color(1.00f, 0.41f, 0.71f);
            doorRenderer.sortingOrder = 60;
            marker.AddComponent<BillboardSprite>();
        }
    }

    private static void BuildPerimeterWalls()
    {
        var root = new GameObject("Perimeter Walls");
        int wallsLayer = Mathf.Max(0, LayerMask.NameToLayer("Walls"));
        Material wall = EnsureMaterial("Wall", new Color(0.35f, 0.29f, 0.24f), unlit: false);

        AddWall(root, wallsLayer, wall, new Vector3(-0.5f, WallHeight * 0.5f, GridH * 0.5f),
            new Vector3(1f, WallHeight, GridH + 2f));
        AddWall(root, wallsLayer, wall, new Vector3(GridW + 0.5f, WallHeight * 0.5f, GridH * 0.5f),
            new Vector3(1f, WallHeight, GridH + 2f));
        AddWall(root, wallsLayer, wall, new Vector3(GridW * 0.5f, WallHeight * 0.5f, -0.5f),
            new Vector3(GridW + 2f, WallHeight, 1f));
        AddWall(root, wallsLayer, wall, new Vector3(GridW * 0.5f, WallHeight * 0.5f, GridH + 0.5f),
            new Vector3(GridW + 2f, WallHeight, 1f));
    }

    private static void AddWall(GameObject parent, int layer, Material material, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.layer = layer;
        wall.transform.SetParent(parent.transform, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void BuildCamera()
    {
        var go = new GameObject("Town Camera", typeof(Camera));
        go.tag = "MainCamera";
        Camera camera = go.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 9f;
        camera.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(-10f, 24f, -10f);
        camera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

        UniversalRendererData data = Isometric3DRendererSetup.Ensure();
        int index = Isometric3DRendererSetup.IndexOf(data);
        if (index >= 0)
        {
            UniversalAdditionalCameraData cameraData = go.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = go.AddComponent<UniversalAdditionalCameraData>();
            cameraData.SetRenderer(index);
        }
    }

    private static void BuildSun()
    {
        var go = new GameObject("Sun", typeof(Light));
        Light light = go.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void BuildPlayer(Vector3 spawn)
    {
        var player = new GameObject("Town Player");
        player.tag = "Player";
        player.transform.position = spawn;

        player.AddComponent<Rigidbody>();
        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.height = 1.6f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0f, 0.8f, 0f);

        player.AddComponent<EntityStats>();
        var controller = player.AddComponent<PlayerController3D>();

        var visual = new GameObject("PlayerVisual");
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(0.20f, 0.60f, 1f);
        renderer.sortingOrder = 100;
        visual.AddComponent<BillboardSprite>();

        SetObjectField(controller, "visualTransform", visual.transform);
    }

    private static int BuildTownNpcs()
    {
        int npcLayer = Mathf.Max(0, LayerMask.NameToLayer("Npc"));
        int wallMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Walls"));

        var cells = new[]
        {
            new Vector2Int(1, 8), new Vector2Int(1, 30),
            new Vector2Int(62, 8), new Vector2Int(62, 30),
            new Vector2Int(16, 1), new Vector2Int(45, 1),
            new Vector2Int(16, 42), new Vector2Int(45, 42),
        };

        var root = new GameObject("Town NPCs");

        for (int i = 0; i < cells.Length; i++)
        {
            var npc = new GameObject("Town NPC " + (i + 1));
            npc.layer = npcLayer;
            npc.transform.SetParent(root.transform, false);
            npc.transform.position = CellToWorld(cells[i].x, cells[i].y);

            npc.AddComponent<Rigidbody>();
            var collider = npc.AddComponent<CapsuleCollider>();
            collider.height = 1.4f;
            collider.radius = 0.35f;
            collider.center = new Vector3(0f, 0.7f, 0f);

            var controller = npc.AddComponent<NpcController>();
            SetStringField(controller, "npcId", "town_npc_" + (i + 1));
            SetStringField(controller, "displayName", "Villager " + (i + 1));

            var wanderer = npc.AddComponent<NpcWander3D>();
            SetIntField(wanderer, "wallLayers", wallMask);
            SetNestedFloat(wanderer, "wanderConfig", "WanderRadius", 8f);
            SetNestedFloat(wanderer, "behaviorConfig", "MoveSpeed", 1.6f);

            var visual = new GameObject("NpcVisual");
            visual.transform.SetParent(npc.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            visual.transform.localScale = Vector3.one * 0.8f;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = new Color(0.95f, 0.62f, 0.20f);
            renderer.sortingOrder = 50;
            visual.AddComponent<BillboardSprite>();
        }

        return cells.Length;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Vector3 CellToWorld(int cx, int cy) =>
        new Vector3((cx + 0.5f) * CellSize, 0f, (cy + 0.5f) * CellSize);

    private static void CreateCellMesh(string name, HashSet<Vector2Int> cells, float height, Material material)
    {
        var vertices = new List<Vector3>(cells.Count * 4);
        var triangles = new List<int>(cells.Count * 6);
        float s = CellSize;

        foreach (Vector2Int cell in cells)
        {
            float x = cell.x * s;
            float z = cell.y * s;
            int b = vertices.Count;
            vertices.Add(new Vector3(x, height, z));
            vertices.Add(new Vector3(x + s, height, z));
            vertices.Add(new Vector3(x + s, height, z + s));
            vertices.Add(new Vector3(x, height, z + s));
            triangles.Add(b + 0); triangles.Add(b + 3); triangles.Add(b + 2);
            triangles.Add(b + 0); triangles.Add(b + 2); triangles.Add(b + 1);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        string meshPath = MaterialsDir + "/" + name + "Mesh.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
            AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
    }

    private static Material EnsureMaterial(string name, Color color, bool unlit)
    {
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
            // Diffuse-only: no specular/environment tint, so the placeholder reads as its base colour.
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_Metallic", 0f);
        }
    }

    private static void ConfigurePlaceholderLighting()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.50f);
    }

    private static void SetObjectField(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetStringField(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetIntField(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetNestedFloat(Object target, string parentField, string childField, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(parentField);
        if (p == null) return;
        SerializedProperty q = p.FindPropertyRelative(childField);
        if (q != null) { q.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetEnumField(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.enumValueIndex = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void AddToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene entry in scenes)
            if (entry.path == scenePath)
                return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
