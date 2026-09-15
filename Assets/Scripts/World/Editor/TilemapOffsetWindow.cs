#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Editor window that shifts the painted tiles of the selected Tilemap(s) by a whole-cell offset.
/// Tiles move through cell coordinates, so transforms stay at (0, 0, 0) and every tilemap keeps
/// sharing the same cells. Scene objects are not moved.
///
/// Unity setup:
///   1. Select one or more Tilemaps (or a Grid) in the Hierarchy.
///   2. Open Tools > World > Tilemap Alignment > Offset Selected Tilemaps.
///   3. Enter Offset X / Offset Y and click Offset Selected. Undo with Ctrl+Z.
///
/// Runtime API: none; this is an editor-only window.
/// </summary>
public class TilemapOffsetWindow : EditorWindow
{
    private int offsetX;
    private int offsetY;
    private bool includeChildren = true;

    [MenuItem("Tools/World/Tilemap Alignment/Offset Selected Tilemaps")]
    private static void Open()
    {
        var window = GetWindow<TilemapOffsetWindow>("Offset Tilemaps");
        window.minSize = new Vector2(320f, 150f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Offset selected tilemap cells", EditorStyles.boldLabel);
        offsetX = EditorGUILayout.IntField("Offset X", offsetX);
        offsetY = EditorGUILayout.IntField("Offset Y", offsetY);
        includeChildren = EditorGUILayout.Toggle("Include child Tilemaps", includeChildren);

        EditorGUILayout.HelpBox(
            "Tiles are moved by cell coordinates; transforms stay at (0,0,0). " +
            "Scene objects (NPCs, doors, portals) are NOT moved.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(offsetX == 0 && offsetY == 0))
        {
            if (GUILayout.Button("Offset Selected"))
                OffsetSelection();
        }
    }

    private void OffsetSelection()
    {
        List<Tilemap> targets = CollectSelectedTilemaps();
        if (targets.Count == 0)
        {
            Debug.LogWarning("[TilemapOffset] Select one or more Tilemaps or a Grid first.");
            return;
        }

        var shift = new Vector2Int(offsetX, offsetY);
        foreach (Tilemap tilemap in targets)
        {
            Undo.RegisterCompleteObjectUndo(tilemap, "Offset Tilemap");
            TilemapAlignmentTool.ShiftTiles(tilemap, shift);
            EditorUtility.SetDirty(tilemap);
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[TilemapOffset] Offset {targets.Count} tilemap(s) by {shift}.");
    }

    private List<Tilemap> CollectSelectedTilemaps()
    {
        var result = new List<Tilemap>();
        foreach (GameObject root in Selection.gameObjects)
        {
            if (root == null)
                continue;

            Tilemap self = root.GetComponent<Tilemap>();
            if (self != null && !result.Contains(self))
                result.Add(self);

            if (!includeChildren)
                continue;

            foreach (Tilemap child in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (!result.Contains(child))
                    result.Add(child);
            }
        }

        return result;
    }
}
#endif
