using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-shot rollout of the NPC/wall layer separation across all scenes and NPC prefabs.
///
/// Assigns every NpcController hierarchy to the `Npc` layer, every collider-bearing Tilemap (walls)
/// to the `Walls` layer, and switches pathfinding/obstacle masks from `Everything` to
/// `~(1 &lt;&lt; Npc)` — i.e. everything except NPC bodies. Walls, props, and the player stay
/// obstacles; NPC bodies no longer block each other's pathfinding. Perception is left permissive
/// (`~0`) so NPCs can still detect the player and gates.
///
/// Unity setup: none. Menu: Tools &gt; World &gt; Apply NPC + Wall Layers. Saves scenes and prefabs.
/// </summary>
public static class NpcLayerRollout
{
    private const int NpcLayerIndex = 6;
    private const int WallsLayerIndex = 7;

    [MenuItem("Tools/World/Apply NPC + Wall Layers")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode first.");

        var report = new StringBuilder();
        report.AppendLine("NPC + wall layer rollout");
        report.AppendLine("Npc layer = " + LayerMask.NameToLayer("Npc") + ", Walls layer = " + LayerMask.NameToLayer("Walls"));

        string currentScene = EditorSceneManager.GetActiveScene().path;

        foreach (string scenePath in EnumerateScenePaths())
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int changed = ApplyToOpenScene(LayerMask.NameToLayer("Npc"), LayerMask.NameToLayer("Walls"));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("scene " + scenePath + " -> changed " + changed);
        }

        foreach (string prefabPath in EnumerateNpcPrefabPaths())
        {
            int changed = ApplyToPrefab(prefabPath, LayerMask.NameToLayer("Npc"));
            report.AppendLine("prefab " + prefabPath + " -> changed " + changed);
        }

        if (!string.IsNullOrEmpty(currentScene))
            EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);

        File.WriteAllText("Temp/layer-rollout.txt", report.ToString());
        Debug.Log("[Layers] Rollout complete.\n" + report);
    }

    private static int ApplyToOpenScene(int npcLayer, int wallsLayer)
    {
        int changed = 0;

        foreach (NpcController npc in Object.FindObjectsByType<NpcController>(FindObjectsInactive.Include))
        {
            changed += AssignLayerToHierarchy(npc.transform, npcLayer);
        }

        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
        {
            if (tilemap.GetComponent<TilemapCollider2D>() != null && tilemap.gameObject.layer != wallsLayer)
            {
                tilemap.gameObject.layer = wallsLayer;
                EditorUtility.SetDirty(tilemap.gameObject);
                changed++;
            }
        }

        changed += ApplyMasks(ExcludedNpcMask(npcLayer));
        return changed;
    }

    private static int ApplyToPrefab(string prefabPath, int npcLayer)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        int changed = 0;
        try
        {
            if (contents.GetComponentInChildren<NpcController>(true) != null)
                changed += AssignLayerToHierarchy(contents.transform, npcLayer);
            changed += ApplyMasks(ExcludedNpcMask(npcLayer), contents);
        }
        finally
        {
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return changed;
    }

    private static int AssignLayerToHierarchy(Transform root, int layer)
    {
        int changed = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.gameObject.layer != layer)
            {
                child.gameObject.layer = layer;
                EditorUtility.SetDirty(child.gameObject);
                changed++;
            }
        }

        return changed;
    }

    /// <summary>Everything except the Npc layer stays an obstacle (walls, props, player).</summary>
    private static int ExcludedNpcMask(int npcLayer) => npcLayer >= 0 ? ~(1 << npcLayer) : ~0;

    private static int ApplyMasks(int mask, GameObject prefabContents = null)
    {
        int changed = 0;
        changed += SetMask(prefabContents, "NpcPathfinder", "obstacleLayers", mask);
        changed += SetMask(prefabContents, "NpcWanderBehavior", "wallLayers", mask);
        changed += SetMask(prefabContents, "NpcDashMeleeController", "obstacleLayers", mask);
        return changed;
    }

    private static int SetMask(GameObject prefabContents, string typeName, string fieldName, int mask)
    {
        MonoBehaviour[] targets = prefabContents != null
            ? prefabContents.GetComponentsInChildren<MonoBehaviour>(true)
            : null;

        int changed = 0;
        if (targets != null)
        {
            foreach (MonoBehaviour mb in targets)
            {
                if (mb != null && mb.GetType().Name == typeName)
                    changed += SetMaskOn(mb, fieldName, mask);
            }
            return changed;
        }

        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (mb != null && mb.GetType().Name == typeName)
                changed += SetMaskOn(mb, fieldName, mask);
        }

        return changed;
    }

    private static int SetMaskOn(MonoBehaviour component, string fieldName, int mask)
    {
        var so = new SerializedObject(component);
        SerializedProperty property = so.FindProperty(fieldName);
        if (property == null || property.intValue == mask)
            return 0;

        property.intValue = mask;
        so.ApplyModifiedPropertiesWithoutUndo();
        return 1;
    }

    private static IEnumerable<string> EnumerateScenePaths()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                yield return path;
        }
    }

    private static IEnumerable<string> EnumerateNpcPrefabPaths()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                yield return path;
        }
    }
}
