#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the reusable SlidingDoor prefab with its root interaction trigger, moving panel,
/// renderer, and solid collider already wired.
///
/// Unity setup: none. The editor ensures Assets/Prefabs/SlidingDoor.prefab exists after
/// compilation. It can also be rebuilt manually from Tools/World/Create Sliding Door Prefab.
/// Existing prefabs are preserved unless the menu command is explicitly used.
///
/// Runtime API: none; this class only runs inside the Unity Editor.
/// </summary>
public static class SlidingDoorPrefabCreator
{
    private const string PrefabDirectory = "Assets/Prefabs";
    private const string PrefabPath = PrefabDirectory + "/SlidingDoor.prefab";

    // Schedules initial prefab creation after Unity finishes compiling scripts.
    [InitializeOnLoadMethod]
    private static void EnsurePrefabExistsAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                CreatePrefab(false);
        };
    }

    // Rebuilds the prefab when selected from Unity's Tools menu.
    [MenuItem("Tools/World/Create Sliding Door Prefab")]
    private static void RebuildPrefabFromMenu() => CreatePrefab(true);

    // Constructs the complete hierarchy, configures serialized references, and saves it.
    private static void CreatePrefab(bool overwrite)
    {
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;

        Directory.CreateDirectory(PrefabDirectory);

        var root = new GameObject("SlidingDoor");
        var interactionTrigger = root.AddComponent<CircleCollider2D>();
        interactionTrigger.isTrigger = true;
        interactionTrigger.radius = 1.1f;
        var door = root.AddComponent<SlidingDoor>();

        var panel = new GameObject("DoorPanel");
        panel.transform.SetParent(root.transform, false);
        panel.transform.localScale = new Vector3(0.9f, 2.2f, 1f);

        var renderer = panel.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Square.png");
        renderer.color = new Color(0.38f, 0.20f, 0.08f, 1f);
        renderer.sortingOrder = 1;

        var blocker = panel.AddComponent<BoxCollider2D>();
        blocker.isTrigger = false;

        var serializedDoor = new SerializedObject(door);
        serializedDoor.FindProperty("slidingPanel").objectReferenceValue = panel.transform;
        serializedDoor.FindProperty("blockingCollider").objectReferenceValue = blocker;
        serializedDoor.FindProperty("openOffset").vector2Value = new Vector2(1.25f, 0f);
        serializedDoor.FindProperty("slideDuration").floatValue = 0.45f;
        serializedDoor.FindProperty("displayName").stringValue = "Door";
        serializedDoor.FindProperty("interactionRange").floatValue = 2f;
        serializedDoor.FindProperty("startsOpen").boolValue = false;
        serializedDoor.FindProperty("canClose").boolValue = true;
        serializedDoor.FindProperty("requiredKeyId").stringValue = "golden_key";
        serializedDoor.FindProperty("consumeKeyOnUnlock").boolValue = false;
        serializedDoor.FindProperty("remainUnlocked").boolValue = true;
        serializedDoor.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (overwrite)
            Selection.activeObject = prefab;

        Debug.Log("[SlidingDoorPrefabCreator] Created " + PrefabPath);
    }
}
#endif
