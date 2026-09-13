#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the reusable grid-aligned SlidingDoor gate prefab and 1-, 2-, 3-, and 4-cell
/// variants. Each gate is two retracting halves built from one diamond sprite per grid cell,
/// with a solid per-cell collider, an interaction trigger, and all SlidingDoor references
/// wired. Cell positions are authored with the canonical isometric step for editor preview;
/// SlidingDoor re-snaps them to the real grid at runtime.
///
/// Unity setup: none. The editor ensures Assets/Prefabs/SlidingDoor.prefab and the four
/// SlidingDoor_*Tile.prefab variants exist after compilation. They can also be rebuilt from
/// Tools/World/Create Sliding Door Prefabs. Existing prefabs are otherwise preserved.
///
/// Runtime API: none; this class only runs inside the Unity Editor.
/// </summary>
public static class SlidingDoorPrefabCreator
{
    private const string PrefabDirectory = "Assets/Prefabs";
    private const string PrefabPath = PrefabDirectory + "/SlidingDoor.prefab";
    private const string GateSpritePath = "Assets/Sprites/Isometric/GateDiamond.png";

    // Schedules initial prefab creation after Unity finishes compiling scripts.
    [InitializeOnLoadMethod]
    private static void EnsurePrefabExistsAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
            {
                CreatePrefab(false);
                CreateGridVariants(false);
            }
        };
    }

    // Rebuilds the base prefab and all grid-sized variants from Unity's Tools menu.
    [MenuItem("Tools/World/Create Sliding Door Prefabs")]
    public static void RebuildAll()
    {
        CreatePrefab(true);
        CreateGridVariants(true);
    }

    private static void CreatePrefab(bool overwrite)
    {
        CreatePrefabAsset(PrefabPath, "SlidingDoor", 1, 1.1f, 1f, "Gate", overwrite);
    }

    private static void CreateGridVariants(bool overwrite)
    {
        for (int tileLength = 1; tileLength <= 4; tileLength++)
        {
            string prefabName = $"SlidingDoor_{tileLength}Tile";
            float interactionRadius = Mathf.Max(1.1f, tileLength * 0.5f + 0.75f);
            CreatePrefabAsset(
                $"{PrefabDirectory}/{prefabName}.prefab",
                prefabName,
                tileLength,
                interactionRadius,
                1f,
                $"{tileLength}-Cell Gate",
                overwrite);
        }
    }

    private static void CreatePrefabAsset(
        string prefabPath,
        string prefabName,
        int cellLength,
        float interactionRadius,
        float interactionRange,
        string displayName,
        bool overwrite)
    {
        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            return;

        Directory.CreateDirectory(PrefabDirectory);

        Sprite gateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GateSpritePath);
        if (gateSprite == null)
            gateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Square.png");

        var root = new GameObject(prefabName);
        var trigger = root.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = interactionRadius;
        var door = root.AddComponent<SlidingDoor>();

        var halfA = new GameObject("GateHalfA");
        halfA.transform.SetParent(root.transform, false);
        var halfB = new GameObject("GateHalfB");
        halfB.transform.SetParent(root.transform, false);

        int mid = cellLength <= 1 ? 1 : cellLength / 2;
        for (int i = 0; i < cellLength; i++)
        {
            var cell = new GameObject($"GateCell_{i}", typeof(SpriteRenderer));
            cell.transform.SetParent(i < mid ? halfA.transform : halfB.transform, false);
            cell.transform.localPosition = CellOffset(i, DoorAxis.GridX);

            var renderer = cell.GetComponent<SpriteRenderer>();
            renderer.sprite = gateSprite;
            renderer.color = new Color(0.95f, 0.28f, 0.16f, 1f);
            renderer.sortingOrder = 2;

            var collider = cell.AddComponent<PolygonCollider2D>();
            collider.SetPath(0, DiamondPoints());
            collider.isTrigger = false;
        }

        var so = new SerializedObject(door);
        so.FindProperty("grid").objectReferenceValue = null;
        so.FindProperty("axis").enumValueIndex = (int)DoorAxis.GridX;
        so.FindProperty("cellLength").intValue = cellLength;
        so.FindProperty("gateSprite").objectReferenceValue = gateSprite;
        so.FindProperty("reverseSlideDirection").boolValue = false;
        so.FindProperty("slideDuration").floatValue = 0.45f;
        so.FindProperty("closeAfterPassing").boolValue = true;
        so.FindProperty("closeDelay").floatValue = 0.25f;
        so.FindProperty("travelerLayers").intValue = ~0;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("interactionRange").floatValue = interactionRange;
        so.FindProperty("startsOpen").boolValue = false;
        so.FindProperty("canClose").boolValue = true;
        so.FindProperty("requiredKeyId").stringValue = "golden_key";
        so.FindProperty("consumeKeyOnUnlock").boolValue = false;
        so.FindProperty("remainUnlocked").boolValue = true;
        so.FindProperty("lockedColor").colorValue = new Color(0.95f, 0.28f, 0.16f, 1f);
        so.FindProperty("unlockedColor").colorValue = new Color(0.22f, 0.9f, 0.82f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (overwrite) Selection.activeObject = prefab;
        Debug.Log("[SlidingDoorPrefabCreator] Created " + prefabPath);
    }

    // Canonical 2:1 isometric cell-center offset for cell index i on the given axis.
    private static Vector3 CellOffset(int i, DoorAxis axis)
    {
        float cx = axis == DoorAxis.GridX ? i : 0f;
        float cy = axis == DoorAxis.GridX ? 0f : i;
        return new Vector3((cx - cy) * 0.5f, (cx + cy) * 0.25f, 0f);
    }

    // Diamond corners for the canonical cell size (1, 0.5).
    private static Vector2[] DiamondPoints()
    {
        return new[]
        {
            new Vector2(0.5f, 0f),
            new Vector2(0f, 0.25f),
            new Vector2(-0.5f, 0f),
            new Vector2(0f, -0.25f)
        };
    }
}
#endif
