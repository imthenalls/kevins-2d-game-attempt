using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Game.Core;

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
        // The main building (index 2) is double-size so it reads as the town landmark.
        (4, 14, 4, 3, 'S'), (12, 14, 4, 3, 'S'), (20, 14, 8, 6, 'S'), (4, 4, 4, 3, 'S'),
        (35, 14, 4, 3, 'S'), (43, 14, 4, 3, 'S'), (51, 14, 4, 3, 'S'), (35, 4, 4, 3, 'S'),
        (4, 24, 4, 3, 'N'), (12, 24, 4, 3, 'N'), (4, 34, 4, 3, 'N'),
        (35, 24, 4, 3, 'N'), (53, 24, 4, 3, 'N'), (35, 36, 4, 3, 'N'),
    };

    private static Scene scene;
    private static Sprite squareSprite;

    // The quest-relevant "main building": a locked front door that requires main_building_key.
    private const int MainBuildingX = 20;
    private const int MainBuildingY = 14;

    private static readonly Dictionary<(int x, int y), Transform> DoorApproaches = new();

    /// <summary>An axis-aligned rectangle of room cells: origin (X, Z), size (W, D).</summary>
    private readonly struct Rect
    {
        public readonly int X, Z, W, D;
        public Rect(int x, int z, int w, int d) { X = x; Z = z; W = w; D = d; }
    }

    /// <summary>
    /// One entrance per building, in the same order as <see cref="Buildings"/>. Rooms are placed south
    /// of town, sized and shaped differently (some L-shaped from two rectangles).
    /// </summary>
    private static readonly Rect[][] Rooms =
    {
        new[] { new Rect(2, -14, 8, 6) },
        new[] { new Rect(14, -13, 6, 5) },
        new[] { new Rect(24, -15, 10, 7) },
        new[] { new Rect(40, -14, 5, 5), new Rect(45, -14, 4, 3) },
        new[] { new Rect(54, -14, 7, 6) },
        new[] { new Rect(2, -28, 9, 8) },
        new[] { new Rect(16, -30, 6, 10) },
        new[] { new Rect(28, -27, 12, 6), new Rect(28, -21, 4, 4) },
        new[] { new Rect(48, -30, 5, 9) },
        new[] { new Rect(2, -44, 7, 7) },
        new[] { new Rect(14, -46, 8, 9), new Rect(22, -46, 4, 4) },
        new[] { new Rect(30, -44, 6, 6) },
        new[] { new Rect(42, -46, 10, 8) },
        new[] { new Rect(58, -44, 8, 6), new Rect(58, -38, 3, 3) },
    };

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
        DoorApproaches.Clear();

        var identity = new GameObject("Town Scene Identity");
        identity.AddComponent<WorldSceneIdentity>();
        SetEnumField(identity.GetComponent<WorldSceneIdentity>(), "world", (int)WorldLayer.WorldA);
        identity.AddComponent<Isometric3DScene>();

        BuildGround();
        BuildRoads();
        BuildPark();
        BuildInteriorGround();
        BuildBuildings();
        BuildInteriors();
        BuildPerimeterWalls();
        BuildTownNpcs();
        BuildTownEnemies();
        BuildCamera();
        BuildSun();

        var spawn = new GameObject("Player Spawn");
        spawn.AddComponent<PlayerSpawnPoint>();
        spawn.transform.position = CellToWorld(10, 10);

        BuildPlayer(spawn.transform.position);
        BuildTownExitPortal();
        BuildHiddenKey();

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
            bool isMainBuilding = b.x == MainBuildingX && b.y == MainBuildingY;

            // A scale-1 container holds the solid body and the door, so the door can live under its
            // building without inheriting the body's non-uniform scale.
            var building = new GameObject(isMainBuilding ? "Main Building" : "Building_" + b.x + "_" + b.y);
            building.transform.SetParent(root.transform, false);
            building.transform.position = new Vector3(centerX, 0f, centerZ);

            GameObject solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solid.name = "Body";
            solid.layer = wallsLayer;
            solid.transform.SetParent(building.transform, false);
            solid.transform.localPosition = new Vector3(0f, BuildingHeight * 0.5f, 0f);
            solid.transform.localScale = new Vector3(b.w, BuildingHeight, b.d);
            solid.GetComponent<MeshRenderer>().sharedMaterial = body;

            int doorCellX = b.x + b.w / 2;
            // 'S' buildings face the street on their +z edge; 'N' buildings on their -z edge.
            float doorX = doorCellX + 0.5f;
            float doorZ = b.side == 'S' ? b.y + b.d + 0.25f : b.y - 0.25f;
            string doorId = isMainBuilding ? "main_building_door" : "door_" + b.x + "_" + b.y;

            var door = new GameObject("Door");
            door.transform.SetParent(building.transform, false);
            door.transform.localPosition = new Vector3(doorX - centerX, 0f, doorZ - centerZ);

            var doorCollider = door.AddComponent<BoxCollider>();
            doorCollider.isTrigger = true;
            doorCollider.size = new Vector3(1.4f, 1.8f, 1.3f);
            doorCollider.center = new Vector3(0f, 0.9f, 0f);

            var portal = door.AddComponent<PortalTrigger3D>();
            SetStringField(portal, "portalId", doorId);
            SetStringField(portal, "destinationPortalId", "int_" + b.x + "_" + b.y);
            // The owner's key opens this door; the main building has its own dedicated key.
            SetStringField(portal, "requiredKeyId", isMainBuilding ? "main_building_key" : "house_key_" + b.x + "_" + b.y);

            // The approach is where a traveler is placed when returning from the room (outside).
            float approachZ = b.side == 'S' ? doorZ + 1.0f : doorZ - 1.0f;
            var approach = new GameObject("Approach");
            approach.transform.SetParent(door.transform, false);
            approach.transform.localPosition = new Vector3(0f, 0f, approachZ - doorZ);
            SetObjectField(portal, "exitPoint", approach.transform);
            DoorApproaches[(b.x, b.y)] = approach.transform;

            // Lay the door flat on the wall face (no billboard), so it reads as part of the building
            // instead of a card turning to face the camera. 0.02 proud avoids z-fighting with the wall.
            float doorVisualZ = b.side == 'S' ? -0.23f : 0.23f;
            var doorVisual = new GameObject("DoorVisual");
            doorVisual.transform.SetParent(door.transform, false);
            doorVisual.transform.localPosition = new Vector3(0f, 0.65f, doorVisualZ);
            doorVisual.transform.localRotation = Quaternion.identity;
            doorVisual.transform.localScale = new Vector3(0.7f, 1.3f, 1f);
            var doorRenderer = doorVisual.AddComponent<SpriteRenderer>();
            doorRenderer.sprite = squareSprite;
            doorRenderer.color = new Color(1.00f, 0.41f, 0.71f);
            doorRenderer.sortingOrder = 60;
        }
    }

    private static void BuildInteriorGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Interior Ground";
        ground.transform.position = new Vector3(GridW * 0.5f, -0.02f, -30f);
        ground.transform.localScale = new Vector3(GridW / 10f, 1f, 7f);
        ground.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial("InteriorGrass", new Color(0.20f, 0.45f, 0.22f), unlit: true);
    }

    private static void BuildInteriors()
    {
        var root = new GameObject("Interiors");
        Material floor = EnsureMaterial("RoomFloor", new Color(0.58f, 0.52f, 0.42f), unlit: true);
        Material wall = EnsureMaterial("Wall", new Color(0.35f, 0.29f, 0.24f), unlit: false);
        int wallsLayer = Mathf.Max(0, LayerMask.NameToLayer("Walls"));

        for (int i = 0; i < Buildings.Count && i < Rooms.Length; i++)
        {
            var b = Buildings[i];
            Rect[] rects = Rooms[i];
            var cells = CellsFrom(rects);
            string roomId = "Room_" + b.x + "_" + b.y;
            string roomName = (b.x == MainBuildingX && b.y == MainBuildingY) ? "Main Building Room" : roomId;

            var room = new GameObject(roomName);
            room.transform.SetParent(root.transform, false);

            CreateCellMesh(roomId + "Floor", cells, 0.03f, floor, room.transform);
            BuildRoomWalls(room.transform, cells, wallsLayer, wall);
            BuildRoomDoor(room.transform, b, rects[0]);
        }
    }

    private static HashSet<Vector2Int> CellsFrom(Rect[] rects)
    {
        var cells = new HashSet<Vector2Int>();
        foreach (Rect r in rects)
        {
            for (int x = r.X; x < r.X + r.W; x++)
                for (int z = r.Z; z < r.Z + r.D; z++)
                    cells.Add(new Vector2Int(x, z));
        }

        return cells;
    }

    private static void BuildRoomDoor(Transform room, (int x, int y, int w, int d, char side) b, Rect main)
    {
        float centerX = main.X + main.W * 0.5f;
        float northWallZ = main.Z + main.D;

        var door = new GameObject("Room Door");
        door.transform.SetParent(room, false);
        door.transform.position = new Vector3(centerX, 0f, northWallZ - 0.6f);

        var collider = door.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1.4f, 1.8f, 1.2f);
        collider.center = new Vector3(0f, 0.8f, 0f);

        var portal = door.AddComponent<PortalTrigger3D>();
        SetStringField(portal, "portalId", "int_" + b.x + "_" + b.y);
        SetStringField(portal, "destinationPortalId",
            (b.x == MainBuildingX && b.y == MainBuildingY) ? "main_building_door" : "door_" + b.x + "_" + b.y);

        var exit = new GameObject("ExitPoint");
        exit.transform.SetParent(door.transform, false);
        exit.transform.position = new Vector3(centerX, 0f, northWallZ - 2.6f);
        SetObjectField(portal, "exitPoint", exit.transform);

        // Flat on the wall plane (no billboard) so the room door matches the town doors.
        var visual = new GameObject("RoomDoorVisual");
        visual.transform.SetParent(door.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(0.7f, 1.3f, 1f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(1.00f, 0.41f, 0.71f);
        renderer.sortingOrder = 60;
    }

    // Builds enclosing walls around an arbitrary cell set, merging collinear edges into single boxes.
    private static void BuildRoomWalls(Transform parent, HashSet<Vector2Int> cells, int layer, Material material)
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

        const float thickness = 0.3f;
        foreach (var kv in north)
        {
            foreach (var run in MergeRuns(kv.Value))
            {
                AddWall(parent, layer, material,
                    new Vector3((run.a + run.b + 1) * 0.5f, WallHeight * 0.5f, kv.Key),
                    new Vector3(run.b - run.a + 1, WallHeight, thickness));
            }
        }
        foreach (var kv in south)
        {
            foreach (var run in MergeRuns(kv.Value))
            {
                AddWall(parent, layer, material,
                    new Vector3((run.a + run.b + 1) * 0.5f, WallHeight * 0.5f, kv.Key),
                    new Vector3(run.b - run.a + 1, WallHeight, thickness));
            }
        }
        foreach (var kv in east)
        {
            foreach (var run in MergeRuns(kv.Value))
            {
                AddWall(parent, layer, material,
                    new Vector3(kv.Key, WallHeight * 0.5f, (run.a + run.b + 1) * 0.5f),
                    new Vector3(thickness, WallHeight, run.b - run.a + 1));
            }
        }
        foreach (var kv in west)
        {
            foreach (var run in MergeRuns(kv.Value))
            {
                AddWall(parent, layer, material,
                    new Vector3(kv.Key, WallHeight * 0.5f, (run.a + run.b + 1) * 0.5f),
                    new Vector3(thickness, WallHeight, run.b - run.a + 1));
            }
        }
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

    private static void SetStringArray(Object target, string field, string[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) return;
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).stringValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildPerimeterWalls()
    {
        var root = new GameObject("Perimeter Walls");
        int wallsLayer = Mathf.Max(0, LayerMask.NameToLayer("Walls"));
        Material wall = EnsureMaterial("Wall", new Color(0.35f, 0.29f, 0.24f), unlit: false);

        AddWall(root.transform, wallsLayer, wall, new Vector3(-0.5f, WallHeight * 0.5f, GridH * 0.5f),
            new Vector3(1f, WallHeight, GridH + 2f));
        AddWall(root.transform, wallsLayer, wall, new Vector3(GridW + 0.5f, WallHeight * 0.5f, GridH * 0.5f),
            new Vector3(1f, WallHeight, GridH + 2f));
        AddWall(root.transform, wallsLayer, wall, new Vector3(GridW * 0.5f, WallHeight * 0.5f, -0.5f),
            new Vector3(GridW + 2f, WallHeight, 1f));
        AddWall(root.transform, wallsLayer, wall, new Vector3(GridW * 0.5f, WallHeight * 0.5f, GridH + 0.5f),
            new Vector3(GridW + 2f, WallHeight, 1f));
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

        var interaction = player.AddComponent<PlayerInteractionController>();
        int npcMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Npc"));
        int interactableLayer = LayerMask.NameToLayer("Interactable");
        int interactableMask = npcMask | (interactableLayer >= 0 ? 1 << interactableLayer : 0);
        SetLayerMaskField(interaction, "npcLayers", npcMask);
        SetLayerMaskField(interaction, "interactableLayers", interactableMask);
        SetNestedFloat(interaction, "config", "InteractionSearchRadius", 2.5f);

        player.AddComponent<CombatReceiver>();
        var playerFlash = player.AddComponent<DamageFlash>();
        SetObjectField(playerFlash, "bodyRenderer", renderer);
        SetColorField(playerFlash, "flashColor", new Color(1f, 0.25f, 0.25f, 1f));
        AddWeaponRig(player, usePlayerInput: true, damage: 12, range: 1.7f, duration: 0.3f, cooldown: 0.45f);
    }

    // A 3D portal back to the 2D Overworld, placed in the open near the spawn. Its Portal Id must
    // match the Overworld portal's Destination Portal Id, and vice versa.
    private static void BuildTownExitPortal()
    {
        var portalObject = new GameObject("Town Exit Portal");
        portalObject.transform.position = CellToWorld(10, 13);

        var collider = portalObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1.6f, 2f, 1.6f);
        collider.center = new Vector3(0f, 1f, 0f);

        var visual = new GameObject("PortalVisual");
        visual.transform.SetParent(portalObject.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(0.45f, 1f, 0.55f);
        renderer.sortingOrder = 50;
        visual.AddComponent<BillboardSprite>();

        var exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(portalObject.transform, false);
        exitPoint.localPosition = new Vector3(0f, 0f, -2f);

        var portal = portalObject.AddComponent<PortalTrigger3D>();
        SetStringField(portal, "portalId", "town_exit");
        SetStringField(portal, "destinationScene", "Overworld");
        SetStringField(portal, "destinationPortalId", "overworld_town");
        SetObjectField(portal, "exitPoint", exitPoint);
    }

    // A walk-over pickup holding the main building key, tucked behind a non-main building so it is
    // the "find it hidden behind a building" alternative to asking the caretaker NPC.
    private static void BuildHiddenKey()
    {
        var pickup = new GameObject("Hidden Main Building Key");
        pickup.transform.position = CellToWorld(6, 2);

        var collider = pickup.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1f, 1f, 1f);
        collider.center = new Vector3(0f, 0.5f, 0f);

        var visual = new GameObject("KeyVisual");
        visual.transform.SetParent(pickup.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        visual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(1f, 0.85f, 0.25f);
        renderer.sortingOrder = 45;
        visual.AddComponent<BillboardSprite>();

        var itemPickup = pickup.AddComponent<ItemPickup>();
        SetStringField(itemPickup, "itemId", "main_building_key");
        SetLayerMaskField(itemPickup, "playerLayers", 1);
    }

    // Adds the equipment, attacker and billboarded 3D weapon rig shared by the player and enemies.
    private static void AddWeaponRig(GameObject entity, bool usePlayerInput, int damage, float range, float duration, float cooldown)
    {
        var equipment = entity.AddComponent<EquipmentManager>();
        SetNestedString(equipment, "config", "StartingWeaponItemId", "iron_sword");

        var attacker = entity.AddComponent<CombatAttacker>();
        SetNestedBool(attacker, "config", "UsePlayerInput", usePlayerInput);
        SetNestedInt(attacker, "config", "AttackDamage", damage);
        SetNestedFloat(attacker, "config", "AttackRange", range);
        SetNestedFloat(attacker, "config", "AttackDuration", duration);
        SetNestedFloat(attacker, "config", "AttackCooldown", cooldown);

        var pivot = new GameObject("WeaponPivot");
        pivot.transform.SetParent(entity.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0.35f, 0f);

        var visual = new GameObject("WeaponVisual");
        visual.transform.SetParent(pivot.transform, false);
        visual.transform.localScale = Vector3.one * 0.3f;
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 110;
        visual.AddComponent<BillboardSprite>();

        var weapon = visual.AddComponent<EquippedWeaponVisual3D>();
        SetObjectField(weapon, "equipmentManager", equipment);
        SetObjectField(weapon, "combatAttacker", attacker);
        SetObjectField(weapon, "swingPivot", pivot.transform);
        SetObjectField(weapon, "weaponRenderer", renderer);
        SetNestedFloat(weapon, "config", "RestYaw", 90f);
        SetNestedFloat(weapon, "config", "SpriteRoll", 150f);
    }

    private static void BuildTownEnemies()
    {
        int npcLayer = Mathf.Max(0, LayerMask.NameToLayer("Npc"));
        int wallMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Walls"));

        var cells = new[]
        {
            new Vector2Int(24, 38),
            new Vector2Int(47, 33),
        };

        var root = new GameObject("Town Enemies");

        for (int i = 0; i < cells.Length; i++)
        {
            var enemy = new GameObject("Town Bandit " + (i + 1));
            enemy.layer = npcLayer;
            enemy.transform.SetParent(root.transform, false);
            enemy.transform.position = CellToWorld(cells[i].x, cells[i].y);

            enemy.AddComponent<Rigidbody>();
            var collider = enemy.AddComponent<CapsuleCollider>();
            collider.height = 1.4f;
            collider.radius = 0.35f;
            collider.center = new Vector3(0f, 0.7f, 0f);

            var npc = enemy.AddComponent<NpcController>();
            SetStringField(npc, "npcId", "town_bandit_" + (i + 1));
            SetStringField(npc, "displayName", "Bandit");
            SetEnumField(npc, "npcType", (int)NpcType.Enemy);
            SetNestedInt(npc, "config", "EnemyMaxHp", 40);
            SetNestedFloat(npc, "config", "InteractionRange", 2.5f);

            enemy.AddComponent<NpcStateView>();
            var wanderer = enemy.AddComponent<NpcWander3D>();
            SetLayerMaskField(wanderer, "wallLayers", wallMask);
            SetLayerMaskField(wanderer, "neighborLayers", 1 << npcLayer);
            SetNestedFloat(wanderer, "wanderConfig", "WanderRadius", 6f);
            SetNestedFloat(wanderer, "behaviorConfig", "MoveSpeed", 1.6f);

            var banditPath = enemy.AddComponent<NpcPathfinder3D>();
            SetLayerMaskField(banditPath, "obstacleLayers", wallMask);
            SetIntField(banditPath, "searchPadding", 8);
            SetIntField(banditPath, "maxNodes", 600);
            enemy.AddComponent<NpcChaseNavigator>();

            AddWeaponRig(enemy, usePlayerInput: false, damage: 8, range: 1.6f, duration: 0.35f, cooldown: 0.9f);
            enemy.AddComponent<NpcProximityMelee3D>();

            var visual = new GameObject("NpcVisual");
            visual.transform.SetParent(enemy.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            visual.transform.localScale = Vector3.one * 0.85f;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = new Color(0.85f, 0.20f, 0.20f);
            renderer.sortingOrder = 55;
            visual.AddComponent<BillboardSprite>();

            var flash = enemy.AddComponent<DamageFlash>();
            SetObjectField(flash, "bodyRenderer", renderer);
        }

        BuildDashEnemy(root.transform, npcLayer, wallMask);
        BuildBruteEnemy(root.transform, npcLayer, wallMask);
    }

    // A slow, high-HP melee brute for variety.
    private static void BuildBruteEnemy(Transform root, int npcLayer, int wallMask)
    {
        var brute = new GameObject("Town Brute");
        brute.layer = npcLayer;
        brute.transform.SetParent(root, false);
        brute.transform.position = CellToWorld(20, 10);

        brute.AddComponent<Rigidbody>();
        var capsule = brute.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.5f;
        capsule.center = new Vector3(0f, 0.9f, 0f);

        var npc = brute.AddComponent<NpcController>();
        SetStringField(npc, "npcId", "town_brute");
        SetStringField(npc, "displayName", "Brute");
        SetEnumField(npc, "npcType", (int)NpcType.Enemy);
        SetNestedInt(npc, "config", "EnemyMaxHp", 90);
        SetNestedFloat(npc, "config", "AggroRange", 9f);
        SetNestedFloat(npc, "config", "InteractionRange", 2.5f);
        brute.AddComponent<NpcStateView>();

        var wanderer = brute.AddComponent<NpcWander3D>();
        SetLayerMaskField(wanderer, "wallLayers", wallMask);
        SetLayerMaskField(wanderer, "neighborLayers", 1 << npcLayer);
        SetNestedFloat(wanderer, "wanderConfig", "WanderRadius", 5f);
        SetNestedFloat(wanderer, "behaviorConfig", "MoveSpeed", 1.2f);

        var brutePath = brute.AddComponent<NpcPathfinder3D>();
        SetLayerMaskField(brutePath, "obstacleLayers", wallMask);
        SetIntField(brutePath, "searchPadding", 8);
        SetIntField(brutePath, "maxNodes", 600);
        brute.AddComponent<NpcChaseNavigator>();

        AddWeaponRig(brute, usePlayerInput: false, damage: 16, range: 1.8f, duration: 0.45f, cooldown: 1.2f);

        var melee = brute.AddComponent<NpcProximityMelee3D>();
        SetFloatField(melee, "chaseSpeed", 1.8f);

        var visual = new GameObject("NpcVisual");
        visual.transform.SetParent(brute.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        visual.transform.localScale = Vector3.one * 1.15f;
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(0.55f, 0.10f, 0.10f);
        renderer.sortingOrder = 56;
        visual.AddComponent<BillboardSprite>();

        var flash = brute.AddComponent<DamageFlash>();
        SetObjectField(flash, "bodyRenderer", renderer);
    }

    // A dash-melee variant: it flashes a warning, then dashes straight at the player.
    private static void BuildDashEnemy(Transform root, int npcLayer, int wallMask)
    {
        var dasher = new GameObject("Town Dasher");
        dasher.layer = npcLayer;
        dasher.transform.SetParent(root, false);
        dasher.transform.position = CellToWorld(42, 10);

        dasher.AddComponent<Rigidbody>();
        var capsule = dasher.AddComponent<CapsuleCollider>();
        capsule.height = 1.4f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0f, 0.7f, 0f);

        var npc = dasher.AddComponent<NpcController>();
        SetStringField(npc, "npcId", "town_dasher");
        SetStringField(npc, "displayName", "Dasher");
        SetEnumField(npc, "npcType", (int)NpcType.Enemy);
        SetNestedInt(npc, "config", "EnemyMaxHp", 50);
        SetNestedFloat(npc, "config", "AggroRange", 14f);
        SetNestedFloat(npc, "config", "InteractionRange", 2.5f);
        dasher.AddComponent<NpcStateView>();

        var dasherPath = dasher.AddComponent<NpcPathfinder3D>();
        SetLayerMaskField(dasherPath, "obstacleLayers", wallMask);
        SetIntField(dasherPath, "searchPadding", 8);
        SetIntField(dasherPath, "maxNodes", 600);
        dasher.AddComponent<NpcChaseNavigator>();

        AddWeaponRig(dasher, usePlayerInput: false, damage: 10, range: 1.6f, duration: 0.3f, cooldown: 0.9f);

        var visual = new GameObject("NpcVisual");
        visual.transform.SetParent(dasher.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        visual.transform.localScale = Vector3.one * 0.85f;
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(0.72f, 0.18f, 0.85f);
        renderer.sortingOrder = 55;
        visual.AddComponent<BillboardSprite>();

        var dash = dasher.AddComponent<NpcDashMelee3D>();
        SetObjectField(dash, "bodyCollider", capsule);
        SetObjectField(dash, "bodyRenderer", renderer);
        SetLayerMaskField(dash, "obstacleLayers", wallMask);
        SetNestedFloat(dash, "config", "WarningDuration", 0.5f);
        SetNestedFloat(dash, "config", "ApproachSpeed", 2.8f);
        SetNestedFloat(dash, "config", "DashRange", 7f);
        SetNestedFloat(dash, "config", "DashSpeed", 14f);
        SetNestedFloat(dash, "config", "StoppingDistance", 1.1f);
        SetNestedFloat(dash, "config", "RecoveryDuration", 1.1f);
    }

    private static int BuildTownNpcs()
    {
        int npcLayer = Mathf.Max(0, LayerMask.NameToLayer("Npc"));
        int wallMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Walls"));

        var cells = new[]
        {
            new Vector2Int(1, 8), new Vector2Int(1, 22), new Vector2Int(1, 30), new Vector2Int(1, 38),
            new Vector2Int(62, 8), new Vector2Int(62, 22), new Vector2Int(62, 30), new Vector2Int(62, 38),
            new Vector2Int(16, 1), new Vector2Int(30, 1), new Vector2Int(45, 1),
            new Vector2Int(16, 42), new Vector2Int(30, 42), new Vector2Int(45, 42),
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
            SetNestedFloat(controller, "config", "InteractionRange", 2.5f);

            var dialogue = npc.AddComponent<NpcDialogue>();
            string dialogueId = i == 0 ? "quest_giver"
                : i == 1 ? "key_holder"
                : (i % 2 == 0) ? "town_villager_a" : "town_villager_b";
            SetStringField(dialogue, "dialogueId", dialogueId);
            if (i == 0)
                SetBoolField(dialogue, "requireHome", true);
            npc.AddComponent<NpcStateView>();

            var wanderer = npc.AddComponent<NpcWander3D>();
            SetLayerMaskField(wanderer, "wallLayers", wallMask);
            SetLayerMaskField(wanderer, "neighborLayers", 1 << npcLayer);
            SetNestedFloat(wanderer, "wanderConfig", "WanderRadius", 8f);
            SetNestedFloat(wanderer, "behaviorConfig", "MoveSpeed", 1.6f);

            // Give the first NPCs a home: they commute to a specific building's door and back.
            // The main building is not a home, so its door's key stays the static quest key.
            if (i < Buildings.Count && !(Buildings[i].x == MainBuildingX && Buildings[i].y == MainBuildingY))
            {
                var b = Buildings[i];
                var pathfinder = npc.AddComponent<NpcPathfinder3D>();
                SetLayerMaskField(pathfinder, "obstacleLayers", wallMask);
                // Home approaches must be standable; do not route to a blocked (inside-wall) cell.
                SetBoolField(pathfinder, "requireWalkableGoal", true);

                string keyId = "house_key_" + b.x + "_" + b.y;
                var schedule = npc.AddComponent<NpcSchedule3D>();
                SetStringField(schedule, "homeDoorPortalId", "door_" + b.x + "_" + b.y);
                SetStringField(schedule, "homeInteriorPortalId", "int_" + b.x + "_" + b.y);
                SetStringField(schedule, "homeKeyId", keyId);
                SetNestedFloat(schedule, "movementConfig", "MoveSpeed", 1.8f);
                SetLayerMaskField(schedule, "neighborLayers", 1 << npcLayer);
                if (DoorApproaches.TryGetValue((b.x, b.y), out Transform approach))
                    SetObjectField(schedule, "homeEntrance", approach);

                // The quest giver goes home quickly and stays there, so the quest is reliably available.
                if (i == 0)
                {
                    SetNestedFloat(schedule, "scheduleConfig", "AwaySeconds", 3f);
                    SetNestedFloat(schedule, "scheduleConfig", "HomeSeconds", 240f);
                }

                // The key is a real item; NpcInventoryDatabase seeds it into this villager's inventory.
            }

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

    private static void CreateCellMesh(string name, HashSet<Vector2Int> cells, float height, Material material, Transform parent = null)
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
        if (parent != null)
            go.transform.SetParent(parent, false);
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

    private static void SetBoolField(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetFloatField(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetColorField(Object target, string field, Color value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.colorValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    // LayerMask serializes as a struct (serializedVersion 2, m_Bits), so intValue does not stick.
    private static void SetLayerMaskField(Object target, string field, int mask)
    {
        var fieldInfo = target.GetType().GetField(
            field,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (fieldInfo == null) return;

        fieldInfo.SetValue(target, (LayerMask)mask);
        UnityEditor.EditorUtility.SetDirty(target);
    }

    private static void SetNestedFloat(Object target, string parentField, string childField, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(parentField);
        if (p == null) return;
        SerializedProperty q = p.FindPropertyRelative(childField);
        if (q != null) { q.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetNestedInt(Object target, string parentField, string childField, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(parentField);
        if (p == null) return;
        SerializedProperty q = p.FindPropertyRelative(childField);
        if (q != null) { q.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetNestedBool(Object target, string parentField, string childField, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(parentField);
        if (p == null) return;
        SerializedProperty q = p.FindPropertyRelative(childField);
        if (q != null) { q.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetNestedString(Object target, string parentField, string childField, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(parentField);
        if (p == null) return;
        SerializedProperty q = p.FindPropertyRelative(childField);
        if (q != null) { q.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
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

