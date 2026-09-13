using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Creates the initial editable WorldB scene with a bounded floor, World B player, camera,
/// arrival/return portal, and standalone portal managers.
///
/// Unity setup: none. Use Tools > Worlds > Create World B Scene. A
/// Temp/world-b-scene-build.request file invokes the same command after compilation without
/// controlling the Unity UI. The builder refuses to replace an existing WorldB scene.
///
/// Runtime API: none; this class is editor-only and writes Assets/Scenes/WorldB.unity.
/// </summary>
public static class WorldBSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/WorldB.unity";
    private const string RequestPath = "Temp/world-b-scene-build.request";
    private const string ResultPath = "Temp/world-b-scene-build.result";

    [InitializeOnLoadMethod]
    private static void Initialize() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;

        File.Delete(RequestPath);
        try
        {
            Build();
            File.WriteAllText(ResultPath, "PASS: WorldB scene created and added to Build Settings.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Worlds/Create World B Scene")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("WorldB scene already exists; refusing to replace it.");

        Scene originalActiveScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        try
        {
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png");
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Settings/squares.asset");
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (square == null || tile == null || material == null)
                throw new InvalidOperationException("WorldB requires the default square sprite and Assets/Settings/squares.asset.");

            GameObject root = new GameObject("World B");
            WorldSceneIdentity identity = root.AddComponent<WorldSceneIdentity>();
            Set(identity, "world", WorldLayer.WorldB);

            BuildMap(root.transform, tile, material);
            BuildPlayer(root.transform, square, material);
            BuildReturnPortal(root.transform, square, material);

            new GameObject("World B Portal Manager").AddComponent<PortalManager>();
            new GameObject("World B Scene Loader").AddComponent<SceneLoader>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Unity could not save the WorldB scene.");

            AddScenesToBuildSettings();
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(originalActiveScene);
        }
    }

    private static void BuildMap(Transform parent, TileBase tile, Material material)
    {
        GameObject gridObject = new GameObject("World B Grid", typeof(Grid));
        gridObject.transform.SetParent(parent, false);

        Tilemap floor = NewMap("Floor", gridObject.transform, new Color(0.12f, 0.20f, 0.30f), -5, material);
        Tilemap walls = NewMap("Walls", gridObject.transform, new Color(0.15f, 0.75f, 0.85f), 1, material);
        walls.gameObject.AddComponent<TilemapCollider2D>();

        const int minX = -10;
        const int maxX = 10;
        const int minY = -7;
        const int maxY = 7;
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            floor.SetTile(new Vector3Int(x, y, 0), tile);
            if (x == minX || x == maxX || y == minY || y == maxY)
                walls.SetTile(new Vector3Int(x, y, 0), tile);
        }

        floor.CompressBounds();
        walls.CompressBounds();
    }

    private static void BuildPlayer(Transform parent, Sprite square, Material material)
    {
        GameObject player = new GameObject("World B Player");
        player.transform.SetParent(parent, false);
        player.transform.position = new Vector3(-6f, 0f, 0f);
        player.tag = "Player";

        Rigidbody2D body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.8f, 0.9f);

        PlayerController2D controller = player.AddComponent<PlayerController2D>();
        player.AddComponent<PlayerInteractionController>();
        WorldCharacter character = player.AddComponent<WorldCharacter>();
        Set(character, "world", WorldLayer.WorldB);
        PlayerAvatarProfile profile = AssetDatabase.LoadAssetAtPath<PlayerAvatarProfile>(
            "Assets/Settings/WorldBPlayerProfile.asset");
        if (profile != null)
            Set(character, "profile", profile);

        GameObject visual = new GameObject("PlayerVisual", typeof(SpriteRenderer));
        visual.transform.SetParent(player.transform, false);
        visual.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.sharedMaterial = material;
        renderer.color = new Color(0.2f, 0.85f, 1f);
        renderer.sortingOrder = 5;
        Set(controller, "visualTransform", visual.transform);

        GameObject cameraObject = new GameObject("World B Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 7.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.08f);
    }

    private static void BuildReturnPortal(Transform parent, Sprite square, Material material)
    {
        GameObject portalObject = new GameObject("World B Entry and Return Portal", typeof(SpriteRenderer));
        portalObject.transform.SetParent(parent, false);
        portalObject.transform.position = new Vector3(7f, 0f, 0f);
        portalObject.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        SpriteRenderer renderer = portalObject.GetComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.sharedMaterial = material;
        renderer.color = new Color(0.8f, 0.25f, 1f);
        renderer.sortingOrder = 3;

        BoxCollider2D trigger = portalObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        Transform exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(portalObject.transform, false);
        exitPoint.localPosition = new Vector3(-1.6f, 0f, 0f);

        PortalTrigger2D portal = portalObject.AddComponent<PortalTrigger2D>();
        Set(portal, "portalId", "world_b_entry");
        Set(portal, "destinationScene", "Overworld");
        Set(portal, "destinationPortalId", "training_hub");
        Set(portal, "changesWorld", true);
        Set(portal, "destinationWorld", WorldLayer.WorldA);
        Set(portal, "exitPoint", exitPoint);
    }

    private static Tilemap NewMap(
        string name,
        Transform parent,
        Color color,
        int sortingOrder,
        Material material)
    {
        GameObject mapObject = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        mapObject.transform.SetParent(parent, false);
        Tilemap map = mapObject.GetComponent<Tilemap>();
        map.color = color;
        TilemapRenderer renderer = mapObject.GetComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.sharedMaterial = material;
        return map;
    }

    private static void AddScenesToBuildSettings()
    {
        var paths = EditorBuildSettings.scenes
            .Where(entry => File.Exists(entry.path))
            .ToDictionary(entry => entry.path, entry => entry.enabled, StringComparer.OrdinalIgnoreCase);
        paths["Assets/Scenes/Overworld.unity"] = true;
        paths[ScenePath] = true;
        EditorBuildSettings.scenes = paths
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new EditorBuildSettingsScene(pair.Key, pair.Value))
            .ToArray();
    }

    private static void Set(UnityEngine.Object target, string fieldName, object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
            throw new InvalidOperationException(target.GetType().Name + "." + fieldName);

        if (value is string text) property.stringValue = text;
        else if (value is bool boolean) property.boolValue = boolean;
        else if (value is Enum enumeration) property.enumValueIndex = Convert.ToInt32(enumeration);
        else if (value is UnityEngine.Object objectReference) property.objectReferenceValue = objectReference;
        else throw new ArgumentException("Unsupported serialized value for " + fieldName);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
