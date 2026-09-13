using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Authors the training wing in Overworld using the existing square tile and Unity's default
/// square sprite. Creates ordinary editable scene objects and a reusable enemy prefab.
/// Unity setup: none. Use Tools > Training Arena > Build Missing Arena in the open Overworld.
/// It refuses to duplicate an existing wing. A Temp/training-arena-build.request file also
/// requests this command after script compilation. It never runs without an explicit request.
/// Runtime API: none; this class is editor-only. Build saves the scene and prefab.
/// </summary>
public static class TrainingArenaBuilder
{
    public const string ArenaRoot = "Training Arena Wing";
    public const string PrefabPath = "Assets/Prefabs/TrainingChallenger.prefab";
    public static readonly Rect ArenaBounds = new Rect(61, -7, 18, 14);
    private const string Request = "Temp/training-arena-build.request";
    private const string VerifyRequest = "Temp/training-arena-verify.request";
    private static Sprite square;
    private static Material material;

    [InitializeOnLoadMethod]
    private static void Initialize() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(VerifyRequest))
        {
            File.Delete(VerifyRequest);
            TrainingArenaVerification.Verify();
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Build(); File.WriteAllText("Temp/training-arena-build.result", "PASS: arena built and scene saved."); }
        catch (Exception e) { File.WriteAllText("Temp/training-arena-build.result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Training Arena/Build Missing Arena")]
    public static void Build()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/Overworld.unity")
            throw new InvalidOperationException("Open Overworld outside Play mode first.");
        if (GameObject.Find(ArenaRoot) != null) throw new InvalidOperationException("Training arena already exists.");
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png");
        material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Settings/squares.asset");
        if (square == null || tile == null) throw new InvalidOperationException("Default square sprite or existing square tile is missing.");

        var root = new GameObject(ArenaRoot);
        Undo.RegisterCreatedObjectUndo(root, "Add training arena");
        var grid = new GameObject("Training Grid", typeof(Grid));
        grid.transform.SetParent(root.transform);
        var floor = NewMap("Floor - small room, hallway, arena", grid.transform, new Color(0.16f, 0.18f, 0.21f), -5);
        var walls = NewMap("Walls", grid.transform, new Color(0.62f, 0.64f, 0.68f), 1);
        walls.gameObject.AddComponent<TilemapCollider2D>();
        var cells = new HashSet<Vector3Int>();
        Fill(cells, 44, -4, 8, 8);
        Fill(cells, 52, -1, 9, 3);
        Fill(cells, 61, -7, 18, 14);
        foreach (Vector3Int cell in cells)
        {
            floor.SetTile(cell, tile);
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    Vector3Int adjacent = cell + new Vector3Int(x, y, 0);
                    if (!cells.Contains(adjacent)) walls.SetTile(adjacent, tile);
                }
        }
        floor.CompressBounds();
        walls.CompressBounds();

        MakePortal("Hub Training Portal", "training_hub", "training_entry", new Vector2(6, 4), root.transform);
        MakePortal("Small Room Return Portal", "training_entry", "training_hub", new Vector2(46, 0), root.transform);
        var giver = Box("Arena Key Keeper", new Vector2(3.8f, -2.38f), Vector2.one, new Color(0.75f, 0.75f, 0.75f), root.transform);
        giver.AddComponent<BoxCollider2D>();
        var npc = giver.AddComponent<NpcController>();
        Set(npc, "npcId", "training_key_keeper"); Set(npc, "displayName", "Arena Keeper");
        var dialogue = giver.AddComponent<NpcDialogue>();
        Set(dialogue, "dialogueId", "training_arena_key_gift"); Set(dialogue, "giftItemId", "training_arena_key");

        var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SlidingDoor.prefab");
        var doorObject = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, scene);
        doorObject.name = "Training Arena Locked Door";
        doorObject.transform.SetParent(root.transform);
        Grid trainingGrid = grid.GetComponent<Grid>();
        doorObject.transform.position = trainingGrid.GetCellCenterWorld(trainingGrid.WorldToCell(doorObject.transform.position));
        doorObject.transform.rotation = Quaternion.identity;
        var door = doorObject.GetComponent<SlidingDoor>();
        Set(door, "requiredKeyId", "training_arena_key"); Set(door, "displayName", "Arena Door");
        Set(door, "grid", trainingGrid);
        Set(door, "cellLength", 3);
        Set(door, "gateSprite", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Isometric/GateDiamond.png"));
        PrefabUtility.RecordPrefabInstancePropertyModifications(door);

        NpcController enemyPrefab = CreateEnemyPrefab();
        var box = Box("Green Training Box", new Vector2(64, 4), Vector2.one, Color.green, root.transform);
        box.AddComponent<BoxCollider2D>();
        var spawnPoint = new GameObject("Challenger Spawn Point").transform;
        spawnPoint.SetParent(root.transform); spawnPoint.position = new Vector3(70, 0, 0);
        var spawner = box.AddComponent<TrainingEnemySpawner>();
        Set(spawner, "enemyPrefab", enemyPrefab); Set(spawner, "spawnPoint", spawnPoint); Set(spawner, "arenaBounds", ArenaBounds);
        // The player's existing high-speed dash also needs continuous collision against the door.
        var playerBody = UnityEngine.Object.FindAnyObjectByType<PlayerController2D>().GetComponent<Rigidbody2D>();
        Undo.RecordObject(playerBody, "Use continuous player collision");
        playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        PortalMapExporter.ExportPortalMap();
        Selection.activeGameObject = root;
    }

    private static NpcController CreateEnemyPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            throw new InvalidOperationException("TrainingChallenger prefab already exists; refusing to replace it.");
        var go = new GameObject("Training Challenger");
        var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var collider = go.AddComponent<BoxCollider2D>(); collider.size = Vector2.one * 0.9f;
        var npc = go.AddComponent<NpcController>();
        Set(npc, "npcId", "training_challenger"); Set(npc, "displayName", "Challenger"); Set(npc, "npcType", 4);
        Set(npc, "enemyMaxHp", 60); Set(npc, "aggroRange", 30f); Set(npc, "showEnemyHealthBar", true);
        var receiver = go.AddComponent<CombatReceiver>();
        var equipment = go.AddComponent<EquipmentManager>(); Set(equipment, "startingWeaponItemId", "iron_sword");
        var attacker = go.AddComponent<CombatAttacker>(); Set(attacker, "usePlayerInput", false); Set(attacker, "attackDuration", 0.35f);
        Set(attacker, "attackCooldown", 0.6f); Set(attacker, "attackDamage", 5);
        var pivot = Box("Aim Pivot", Vector2.zero, Vector2.one, new Color(0.75f, 0.2f, 0.2f), go.transform);
        var weapon = new GameObject("WeaponVisual", typeof(SpriteRenderer));
        weapon.transform.SetParent(pivot.transform, false); weapon.transform.localPosition = new Vector3(0.62f, 0.17f, 0);
        weapon.transform.localScale = new Vector3(0.8f, 0.8f, 1);
        weapon.GetComponent<SpriteRenderer>().sharedMaterial = material;
        weapon.GetComponent<SpriteRenderer>().sortingOrder = 4;
        var visual = weapon.AddComponent<EquippedWeaponVisual>();
        Set(visual, "equipmentManager", equipment); Set(visual, "combatAttacker", attacker);
        Set(visual, "weaponRenderer", weapon.GetComponent<SpriteRenderer>()); Set(visual, "swingTransform", weapon.transform);
        Set(visual, "facingRenderer", pivot.GetComponent<SpriteRenderer>());
        var ai = go.AddComponent<NpcDashMeleeController>();
        Set(ai, "bodyCollider", collider); Set(ai, "bodyRenderer", pivot.GetComponent<SpriteRenderer>()); Set(ai, "aimPivot", pivot.transform);
        Set(ai, "arenaBounds", ArenaBounds);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab.GetComponent<NpcController>();
    }

    private static Tilemap NewMap(string name, Transform parent, Color color, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(parent, false);
        var map = go.GetComponent<Tilemap>(); map.color = color;
        var renderer = go.GetComponent<TilemapRenderer>(); renderer.sortingOrder = order; renderer.sharedMaterial = material;
        return map;
    }
    private static void Fill(HashSet<Vector3Int> cells, int x, int y, int width, int height)
    {
        for (int yy = y; yy < y + height; yy++) for (int xx = x; xx < x + width; xx++) cells.Add(new Vector3Int(xx, yy, 0));
    }
    private static GameObject Box(string name, Vector2 position, Vector2 scale, Color color, Transform parent)
    {
        var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = new Vector3(scale.x, scale.y, 1);
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = square; renderer.color = color;
        renderer.sharedMaterial = material; renderer.sortingOrder = 2;
        return go;
    }
    private static void MakePortal(string name, string id, string destination, Vector2 position, Transform parent)
    {
        var go = Box(name, position, new Vector2(1.2f, 1.2f), new Color(0.25f, 0.6f, 1f), parent);
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        var exit = new GameObject("ExitPoint").transform; exit.SetParent(go.transform, false); exit.localPosition = new Vector3(1.6f, 0, 0);
        var portal = go.AddComponent<PortalTrigger2D>(); Set(portal, "portalId", id); Set(portal, "destinationPortalId", destination); Set(portal, "exitPoint", exit);
    }
    public static void Set(UnityEngine.Object target, string field, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(target.GetType().Name + "." + field);
        if (value is string s) p.stringValue = s;
        else if (value is bool b) p.boolValue = b;
        else if (value is int i) p.intValue = i;
        else if (value is float f) p.floatValue = f;
        else if (value is Vector2 v) p.vector2Value = v;
        else if (value is Rect r) p.rectValue = r;
        else if (value is UnityEngine.Object o) p.objectReferenceValue = o;
        else throw new ArgumentException("Unsupported serialized value");
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
