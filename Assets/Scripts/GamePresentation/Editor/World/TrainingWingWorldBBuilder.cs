#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the isometric training wing inside the WorldB scene: an entrance room, a hallway with a
/// locked gate, and a combat arena, plus the Arena Key Keeper, Green Training Box spawner, and
/// challenger spawn point. The WorldB player, camera, and generated room are left untouched.
/// It also points the WorldB arrival portal back to Overworld so the two-world round trip works.
///
/// Unity setup: none. Open Assets/Scenes/WorldB.unity, then use
/// Tools > Worlds > Build Training Wing In WorldB. The builder refuses to duplicate an existing wing.
///
/// Runtime API: none; this class is editor-only.
/// </summary>
public static class TrainingWingWorldBBuilder
{
    public const string WorldBPath = "Assets/Scenes/WorldB.unity";
    public const string WingRoot = "Training Wing";

    private const string FloorTilePath = "Assets/Tiles/Isometric/FloorDiamond.asset";
    private const string WallTilePath = "Assets/Tiles/Isometric/WallDiamond.asset";
    private const string GateSpritePath = "Assets/Sprites/Isometric/GateDiamond.png";
    private const string DoorPrefabPath = "Assets/Prefabs/SlidingDoor.prefab";
    private const string ChallengerPrefabPath = "Assets/Prefabs/TrainingChallenger.prefab";
    private const string OverworldEntryPortalId = "world_b_portal";

    [MenuItem("Tools/Worlds/Build Training Wing In WorldB")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != WorldBPath)
            throw new InvalidOperationException("Open Assets/Scenes/WorldB.unity before building the training wing.");
        if (GameObject.Find(WingRoot) != null)
            throw new InvalidOperationException("The training wing already exists in this scene.");

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png");
        Tile floorTile = AssetDatabase.LoadAssetAtPath<Tile>(FloorTilePath);
        Tile wallTile = AssetDatabase.LoadAssetAtPath<Tile>(WallTilePath);
        Sprite gateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GateSpritePath);
        Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        if (square == null || floorTile == null || wallTile == null || material == null)
            throw new InvalidOperationException("Training wing needs the iso tiles, default square sprite, and default material.");

        GameObject root = new GameObject(WingRoot);
        Undo.RegisterCreatedObjectUndo(root, "Build training wing");

        // Isometric grid.
        var gridObject = new GameObject("Training Grid", typeof(Grid));
        gridObject.transform.SetParent(root.transform, false);
        Grid grid = gridObject.GetComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize = new Vector3(1f, 0.5f, 0f);

        Tilemap floor = NewMap("Floor", gridObject.transform, floorTile, -100);
        Tilemap walls = NewMap("Walls", gridObject.transform, wallTile, -50);
        walls.gameObject.AddComponent<TilemapCollider2D>();

        var areas = new List<RectInt>
        {
            new RectInt(44, -4, 8, 8),   // entrance room
            new RectInt(52, -1, 9, 3),   // hallway
            new RectInt(61, -7, 18, 14), // arena
        };

        var floorCells = new HashSet<Vector3Int>();
        foreach (RectInt area in areas)
            Fill(floorCells, area);

        foreach (Vector3Int cell in floorCells)
        {
            floor.SetTile(cell, floorTile);
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                var neighbour = cell + new Vector3Int(x, y, 0);
                if (!floorCells.Contains(neighbour))
                    walls.SetTile(neighbour, wallTile);
            }
        }
        floor.CompressBounds();
        walls.CompressBounds();

        // Locked gate across the hallway (three cells along the grid Y axis at x = 56).
        GameObject doorObject = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath), scene);
        doorObject.name = "Training Arena Locked Door";
        doorObject.transform.SetParent(root.transform, false);
        var door = doorObject.GetComponent<SlidingDoor>();
        Set(door, "grid", grid);
        SetEnum(door, "axis", (int)DoorAxis.GridY);
        Set(door, "cellLength", 3);
        Set(door, "gateSprite", gateSprite);
        Set(door, "requiredKeyId", "training_arena_key");
        Set(door, "displayName", "Arena Gate");
        doorObject.transform.position = grid.GetCellCenterWorld(new Vector3Int(56, -1, 0));

        // Arena Key Keeper in the entrance room.
        GameObject keeper = Box("Arena Key Keeper", grid.GetCellCenterWorld(new Vector3Int(45, 2, 0)),
            Vector2.one, new Color(0.75f, 0.75f, 0.75f), root.transform, square, material);
        keeper.AddComponent<BoxCollider2D>();
        var keeperNpc = keeper.AddComponent<NpcController>();
        Set(keeperNpc, "npcId", "training_key_keeper");
        Set(keeperNpc, "displayName", "Arena Keeper");
        var keeperDialogue = keeper.AddComponent<NpcDialogue>();
        Set(keeperDialogue, "dialogueId", "training_arena_key_gift");
        Set(keeperDialogue, "giftItemId", "training_arena_key");

        // Challenger spawn point and green box in the arena.
        var spawnPoint = new GameObject("Challenger Spawn Point").transform;
        spawnPoint.SetParent(root.transform, false);
        spawnPoint.position = grid.GetCellCenterWorld(new Vector3Int(70, 0, 0));

        Rect arenaBounds = CellBounds(grid, new RectInt(61, -7, 18, 14));
        GameObject box = Box("Green Training Box", grid.GetCellCenterWorld(new Vector3Int(64, 4, 0)),
            Vector2.one, Color.green, root.transform, square, material);
        box.AddComponent<BoxCollider2D>();
        var spawner = box.AddComponent<TrainingEnemySpawner>();
        Set(spawner, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ChallengerPrefabPath)?.GetComponent<NpcController>());
        Set(spawner, "spawnPoint", spawnPoint);
        Set(spawner, "arenaBounds", arenaBounds);

        // Keep the WorldB player's dash from skipping through the closed gate.
        PlayerController2D player = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>();
        if (player != null)
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        // Point the WorldB arrival portal at the wing entrance and back to Overworld.
        ConfigureArrivalPortal(grid);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        PortalMapExporter.ExportPortalMap();
        Selection.activeGameObject = root;
    }

    private static void ConfigureArrivalPortal(Grid grid)
    {
        GameObject portalGo = GameObject.Find("World B Entry and Return Portal");
        if (portalGo == null)
            portalGo = GameObject.Find("world_b_entry");

        if (portalGo == null)
        {
            Debug.LogWarning("[TrainingWingWorldBBuilder] No WorldB arrival portal found; create one at 'world_b_entry'.");
            return;
        }

        portalGo.transform.position = grid.GetCellCenterWorld(new Vector3Int(47, 0, 0));
        var portal = portalGo.GetComponent<PortalTrigger2D>();
        Transform exit = portalGo.transform.Find("ExitPoint");
        if (exit == null)
        {
            exit = new GameObject("ExitPoint").transform;
            exit.SetParent(portalGo.transform, false);
        }
        exit.localPosition = new Vector3(-0.6f, 0.6f, 0f);

        Set(portal, "portalId", "world_b_entry");
        Set(portal, "destinationScene", "Overworld");
        Set(portal, "destinationPortalId", OverworldEntryPortalId);
        Set(portal, "changesWorld", true);
        SetEnum(portal, "destinationWorld", (int)WorldLayer.WorldA);
        Set(portal, "exitPoint", exit);
    }

    private static Rect CellBounds(Grid grid, RectInt area)
    {
        Vector3 min = grid.CellToWorld(new Vector3Int(area.xMin, area.yMin, 0));
        Vector3 max = grid.CellToWorld(new Vector3Int(area.xMax, area.yMax, 0));
        return Rect.MinMaxRect(
            Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
            Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
    }

    private static void Fill(HashSet<Vector3Int> cells, RectInt area)
    {
        for (int y = area.yMin; y < area.yMax; y++)
        for (int x = area.xMin; x < area.xMax; x++)
            cells.Add(new Vector3Int(x, y, 0));
    }

    private static Tilemap NewMap(string name, Transform parent, TileBase tile, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        var map = go.GetComponent<Tilemap>();
        map.color = Color.white;
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return map;
    }

    private static GameObject Box(string name, Vector3 position, Vector2 scale, Color color,
        Transform parent, Sprite square, Material material)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.color = color;
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 2;
        return go;
    }

    private static void Set(UnityEngine.Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(target.GetType().Name + "." + field);
        if (value is string s) p.stringValue = s;
        else if (value is bool b) p.boolValue = b;
        else if (value is int i) p.intValue = i;
        else if (value is float f) p.floatValue = f;
        else if (value is Vector2 v) p.vector2Value = v;
        else if (value is Rect r) p.rectValue = r;
        else if (value is UnityEngine.Object o) p.objectReferenceValue = o;
        else throw new ArgumentException("Unsupported serialized value for " + field);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetEnum(UnityEngine.Object target, string field, int index)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(target.GetType().Name + "." + field);
        p.enumValueIndex = index;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
