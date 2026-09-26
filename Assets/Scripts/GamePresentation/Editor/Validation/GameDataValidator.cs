using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor validation for the string-id data the game depends on at runtime. Items, dialogue,
/// NPC inventories, enemy loot, and quests are authored as JSON in Assets/StreamingAssets, and NPC /
/// portal ids live in scenes; a typo in any of them otherwise fails only at runtime.
///
/// Checks: blank/duplicate ids, dangling item/quest/node/portal references, dialogue node links,
/// quest node transitions, and per-scene duplicate NPC ids.
///
/// Unity setup: none. Menu: Tools &gt; Validation &gt; Validate Game Data. Also callable from code as
/// GameDataValidator.Validate(). Writes a report to Temp/game-data-validation.txt and logs a summary.
/// Scene checks are skipped (with a warning) when any open scene has unsaved changes.
/// </summary>
public static class GameDataValidator
{
    private const string ReportPath = "Temp/game-data-validation.txt";
    private static readonly string[] SceneFolders = { "Assets/Scenes" };

    private static readonly HashSet<string> KnownConditionTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "ObjectiveComplete", "Fact", "QuestInNode", "HasItem",
    };

    private static readonly HashSet<string> KnownActionTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "SetFact", "ClearFlag", "ToggleFlag", "GiveItem", "RemoveItem",
        "StartQuest", "ClaimRewards", "GrantMana",
    };

    [MenuItem("Tools/Validation/Validate Game Data")]
    public static void ValidateMenu() => Validate();

    /// <summary>Runs all checks; returns the number of issues found (0 = clean).</summary>
    public static int Validate()
    {
        var issues = new List<ValidationIssue>();
        string dataRoot = Path.Combine(Application.dataPath, "StreamingAssets");

        // 1. Items — the id space everything else references.
        var itemIds = new List<string>();
        ReadItemIds(Path.Combine(dataRoot, "items.json"), itemIds, issues);

        // 2. Quests — collect ids and validate node/transition/reference integrity.
        Dictionary<string, QuestGraphData> quests = LoadQuests(issues);
        var questIds = quests.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        ValidateQuests(quests, itemIds, issues);

        // 3. Dialogue — ids, node links, and references to quests.
        var dialogueRefs = new List<IdIntegrity.IdReference>();
        var sceneRefs = new List<IdIntegrity.IdReference>();
        ReadDialogues(Path.Combine(dataRoot, "dialogues.json"), questIds, dialogueRefs, sceneRefs, issues);

        // 4. NPC inventories and enemy loot reference item ids.
        var itemRefs = new List<IdIntegrity.IdReference>();
        ReadNpcInventories(Path.Combine(dataRoot, "npc_inventories.json"), itemRefs, issues);
        ReadEnemyLoot(Path.Combine(dataRoot, "enemy_loot.json"), itemRefs, issues);

        issues.AddRange(IdIntegrity.FindDanglingReferences(itemRefs, itemIds, "item"));
        issues.AddRange(IdIntegrity.FindDanglingReferences(dialogueRefs, questIds, "quest"));

        // 5. Scenes — duplicate/blank NPC ids and portal destination integrity.
        var sceneNames = new List<string>();
        var portalIds = new List<string>();
        var portalRefs = new List<IdIntegrity.IdReference>();
        ScanScenes(sceneNames, portalIds, portalRefs, issues);
        issues.AddRange(IdIntegrity.FindDanglingReferences(sceneRefs, sceneNames, "scene"));
        issues.AddRange(IdIntegrity.FindDanglingReferences(portalRefs, portalIds, "portal"));

        WriteReport(issues);
        return issues.Count(i => i.Severity == ValidationSeverity.Error || i.Severity == ValidationSeverity.Warning);
    }

    // ── Items ────────────────────────────────────────────────────────────────

    private static void ReadItemIds(string path, List<string> itemIds, List<ValidationIssue> issues)
    {
        ItemsFile file = ReadJson<ItemsFile>(path, issues);
        if (file?.items == null)
            return;

        foreach (ItemRecord item in file.items)
            itemIds.Add(item != null ? item.id : null);

        issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(itemIds, "item"));
    }

    // ── Quests ───────────────────────────────────────────────────────────────

    private static Dictionary<string, QuestGraphData> LoadQuests(List<ValidationIssue> issues)
    {
        try
        {
            return QuestLoader.LoadAll() ?? new Dictionary<string, QuestGraphData>();
        }
        catch (Exception e)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "quest.load", "Failed to load quests: " + e.Message));
            return new Dictionary<string, QuestGraphData>();
        }
    }

    private static void ValidateQuests(
        Dictionary<string, QuestGraphData> quests,
        IReadOnlyCollection<string> itemIds,
        List<ValidationIssue> issues)
    {
        var questIdList = new List<string>(quests.Keys);
        issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(questIdList, "quest"));

        var itemRefs = new List<IdIntegrity.IdReference>();
        var questRefs = new List<IdIntegrity.IdReference>();

        foreach (KeyValuePair<string, QuestGraphData> pair in quests)
        {
            QuestGraphData quest = pair.Value;
            if (quest == null)
                continue;

            string questLabel = "quest '" + (quest.questId ?? pair.Key) + "'";
            var nodeIds = quest.nodes?.Select(n => n?.id).ToList() ?? new List<string>();
            issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(nodeIds, questLabel + " node"));

            var nodeSet = new HashSet<string>(nodeIds.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(quest.startNodeId) && !nodeSet.Contains(quest.startNodeId))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, "quest.start",
                    questLabel + " startNodeId '" + quest.startNodeId + "' does not exist."));
            }

            foreach (QuestNodeData node in quest.nodes ?? Enumerable.Empty<QuestNodeData>())
            {
                if (node == null)
                    continue;

                string owner = questLabel + " node '" + node.id + "'";

                foreach (QuestTransitionData transition in node.transitions ?? Enumerable.Empty<QuestTransitionData>())
                {
                    if (transition != null && !string.IsNullOrWhiteSpace(transition.targetNodeId) && !nodeSet.Contains(transition.targetNodeId))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, "quest.transition",
                            owner + " transitions to missing node '" + transition.targetNodeId + "'."));
                    }
                }

                foreach (QuestConditionData condition in CollectConditions(node))
                {
                    if (condition == null)
                        continue;
                    if (!string.IsNullOrWhiteSpace(condition.type) && !KnownConditionTypes.Contains(condition.type))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, "quest.condition.type",
                            owner + " uses unknown condition type '" + condition.type + "'."));
                    }
                    if (!string.IsNullOrWhiteSpace(condition.itemId))
                        itemRefs.Add(new IdIntegrity.IdReference(owner, condition.itemId));
                    if (!string.IsNullOrWhiteSpace(condition.questId))
                        questRefs.Add(new IdIntegrity.IdReference(owner, condition.questId));
                    if (!string.IsNullOrWhiteSpace(condition.nodeId) && !nodeSet.Contains(condition.nodeId))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, "quest.condition.node",
                            owner + " condition targets missing node '" + condition.nodeId + "'."));
                    }
                }

                foreach (QuestActionData action in node.onEnterActions ?? Enumerable.Empty<QuestActionData>())
                {
                    if (action == null)
                        continue;
                    if (!string.IsNullOrWhiteSpace(action.type) && !KnownActionTypes.Contains(action.type))
                    {
                        issues.Add(new ValidationIssue(
                            ValidationSeverity.Error, "quest.action.type",
                            owner + " uses unknown action type '" + action.type + "'."));
                    }
                    if (!string.IsNullOrWhiteSpace(action.itemId))
                        itemRefs.Add(new IdIntegrity.IdReference(owner, action.itemId));
                    if (!string.IsNullOrWhiteSpace(action.questId))
                        questRefs.Add(new IdIntegrity.IdReference(owner, action.questId));
                }
            }
        }

        issues.AddRange(IdIntegrity.FindDanglingReferences(itemRefs, itemIds, "item"));
        issues.AddRange(IdIntegrity.FindDanglingReferences(questRefs, questIdList, "quest"));
    }

    private static IEnumerable<QuestConditionData> CollectConditions(QuestNodeData node)
    {
        foreach (QuestTransitionData transition in node.transitions ?? Enumerable.Empty<QuestTransitionData>())
        {
            foreach (QuestConditionData condition in transition?.conditions ?? Enumerable.Empty<QuestConditionData>())
                yield return condition;
        }
    }

    // ── Dialogue ─────────────────────────────────────────────────────────────

    private static void ReadDialogues(
        string path,
        IReadOnlyCollection<string> knownQuests,
        List<IdIntegrity.IdReference> questRefs,
        List<IdIntegrity.IdReference> sceneRefs,
        List<ValidationIssue> issues)
    {
        DialogueFile file = ReadJson<DialogueFile>(path, issues);
        if (file?.dialogues == null)
            return;

        issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(
            file.dialogues.Select(d => d?.dialogueId).ToList(), "dialogue"));

        foreach (DialogueGraph graph in file.dialogues)
        {
            if (graph == null)
                continue;

            string label = "dialogue '" + graph.dialogueId + "'";
            var nodeIds = graph.nodes?.Select(n => n?.id).ToList() ?? new List<string>();
            issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(nodeIds, label + " node"));
            var nodeSet = new HashSet<string>(nodeIds.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(graph.startNodeId) && !nodeSet.Contains(graph.startNodeId))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error, "dialogue.start",
                    label + " startNodeId '" + graph.startNodeId + "' does not exist."));
            }

            foreach (DialogueNode node in graph.nodes ?? Enumerable.Empty<DialogueNode>())
            {
                if (node == null)
                    continue;

                string owner = label + " node '" + node.id + "'";
                CheckLink(owner, node.nextNodeId, node.endConversation, nodeSet, issues);

                foreach (DialogueChoice choice in node.choices ?? Enumerable.Empty<DialogueChoice>())
                {
                    if (choice == null)
                        continue;
                    CheckLink(owner + " choice", choice.nextNodeId, choice.endConversation, nodeSet, issues);

                    if (!string.IsNullOrWhiteSpace(choice.questId))
                        questRefs.Add(new IdIntegrity.IdReference(owner, choice.questId));
                    if (!string.IsNullOrWhiteSpace(choice.teleportScene))
                        sceneRefs.Add(new IdIntegrity.IdReference(owner, choice.teleportScene));
                }
            }
        }
    }

    private static void CheckLink(
        string owner,
        string nextNodeId,
        bool endConversation,
        HashSet<string> nodeSet,
        List<ValidationIssue> issues)
    {
        if (endConversation || string.IsNullOrWhiteSpace(nextNodeId))
            return;
        if (!nodeSet.Contains(nextNodeId))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error, "dialogue.link",
                owner + " links to missing node '" + nextNodeId + "'."));
        }
    }

    // ── NPC inventories and enemy loot ───────────────────────────────────────

    private static void ReadNpcInventories(string path, List<IdIntegrity.IdReference> itemRefs, List<ValidationIssue> issues)
    {
        NpcInventoryFile file = ReadJson<NpcInventoryFile>(path, issues);
        if (file?.npcInventories == null)
            return;

        issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(
            file.npcInventories.Select(e => e?.npcId).ToList(), "npc inventory"));

        foreach (NpcInventoryEntry entry in file.npcInventories)
        {
            if (entry?.items == null)
                continue;
            foreach (NpcInventoryItem item in entry.items)
                itemRefs.Add(new IdIntegrity.IdReference("npc inventory '" + entry.npcId + "'", item?.itemId));
        }
    }

    private static void ReadEnemyLoot(string path, List<IdIntegrity.IdReference> itemRefs, List<ValidationIssue> issues)
    {
        EnemyLootFile file = ReadJson<EnemyLootFile>(path, issues);
        if (file?.enemyLoot == null)
            return;

        issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(
            file.enemyLoot.Select(e => e?.npcId).ToList(), "enemy loot"));

        foreach (EnemyLootEntry entry in file.enemyLoot)
        {
            if (entry?.items == null)
                continue;
            foreach (EnemyLootItem item in entry.items)
                itemRefs.Add(new IdIntegrity.IdReference("enemy loot '" + entry.npcId + "'", item?.itemId));
        }
    }

    // ── Scenes ───────────────────────────────────────────────────────────────

    private static void ScanScenes(
        List<string> sceneNames,
        List<string> portalIds,
        List<IdIntegrity.IdReference> portalRefs,
        List<ValidationIssue> issues)
    {
        foreach (string path in EnumerateScenePaths())
            sceneNames.Add(Path.GetFileNameWithoutExtension(path));

        if (EditorSceneManager.GetActiveScene().isDirty || AnyOpenSceneDirty())
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning, "scene.dirty",
                "Skipped scene checks: save open scenes first."));
            return;
        }

        foreach (string path in EnumerateScenePaths())
        {
            var npcIds = new List<string>();
            var sceneName = Path.GetFileNameWithoutExtension(path);

            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool is3D = UnityEngine.Object.FindObjectsByType<Isometric3DScene>(FindObjectsInactive.Include).Length > 0;
                foreach (NpcController npc in UnityEngine.Object.FindObjectsByType<NpcController>())
                    npcIds.Add(npc.NpcId);

                foreach (PortalTrigger2D portal in UnityEngine.Object.FindObjectsByType<PortalTrigger2D>(FindObjectsInactive.Include))
                {
                    if (string.IsNullOrWhiteSpace(portal.PortalId))
                        continue;
                    portalIds.Add(portal.PortalId);
                    if (!string.IsNullOrWhiteSpace(portal.DestinationPortalId))
                        portalRefs.Add(new IdIntegrity.IdReference("portal '" + portal.PortalId + "'", portal.DestinationPortalId));
                }

                foreach (PortalTrigger3D portal in UnityEngine.Object.FindObjectsByType<PortalTrigger3D>(FindObjectsInactive.Include))
                {
                    if (string.IsNullOrWhiteSpace(portal.PortalId))
                        continue;
                    portalIds.Add(portal.PortalId);
                    if (!string.IsNullOrWhiteSpace(portal.DestinationPortalId))
                        portalRefs.Add(new IdIntegrity.IdReference("portal '" + portal.PortalId + "'", portal.DestinationPortalId));
                }

                issues.AddRange(IdIntegrity.FindDuplicateOrBlankIds(npcIds, "scene '" + sceneName + "' npc"));

                int worldCharacters = UnityEngine.Object.FindObjectsByType<WorldCharacter>(FindObjectsInactive.Include).Length;
                int identities = UnityEngine.Object.FindObjectsByType<WorldSceneIdentity>(FindObjectsInactive.Include).Length;
                if (worldCharacters > 0 && identities == 0)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, "scene.worldIdentity",
                        "Scene '" + sceneName + "' has a WorldCharacter but no WorldSceneIdentity; a direct " +
                        "scene load can leave the wrong world active and deactivate the player."));
                }
                else if (identities > 1)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning, "scene.worldIdentity.multiple",
                        "Scene '" + sceneName + "' has " + identities + " WorldSceneIdentity components; keep exactly one."));
                }
                int spawnPoints = UnityEngine.Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include).Length;
                if (identities > 0 && spawnPoints == 0)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error, "scene.spawnPoint",
                        "Scene '" + sceneName + "' has no PlayerSpawnPoint; the player has no defined start position."));
                }
                else if (spawnPoints > 1)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Warning, "scene.spawnPoint.multiple",
                        "Scene '" + sceneName + "' has " + spawnPoints + " PlayerSpawnPoints; keep one default."));
                }
                // Grid + tilemap transform hygiene, a MainCamera, and the Walls-layer convention.
                // 3D planar-isometric scenes are real geometry, not tilemaps, so these are exempt.
                if (!is3D)
                {
                var grids = UnityEngine.Object.FindObjectsByType<Grid>(FindObjectsInactive.Include);
                if (grids.Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "scene.grid",
                        "Scene '" + sceneName + "' has no Grid."));
                }

                foreach (Grid grid in grids)
                {
                    Transform gridTransform = grid.transform;
                    if (gridTransform.position != Vector3.zero || gridTransform.rotation != Quaternion.identity ||
                        gridTransform.localScale != Vector3.one)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, "scene.grid.transform",
                            "Scene '" + sceneName + "' Grid '" + grid.name +
                            "' must be at the origin with identity rotation and scale 1."));
                    }

                    if (grid.cellLayout != GridLayout.CellLayout.Isometric ||
                        grid.cellSize != new Vector3(1f, 0.5f, 0f))
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning, "scene.grid.isometric",
                            "Scene '" + sceneName + "' Grid '" + grid.name +
                            "' is not the project's isometric cell (1, 0.5)."));
                    }
                }

                int wallsLayerIndex = LayerMask.NameToLayer("Walls");
                foreach (UnityEngine.Tilemaps.Tilemap map in
                    UnityEngine.Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsInactive.Include))
                {
                    Transform mapTransform = map.transform;
                    if (mapTransform.position != Vector3.zero || mapTransform.rotation != Quaternion.identity ||
                        mapTransform.localScale != Vector3.one)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, "scene.tilemap.transform",
                            "Scene '" + sceneName + "' Tilemap '" + map.name +
                            "' must be at the origin with identity rotation and scale 1."));
                    }

                    if (map.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>() != null &&
                        map.gameObject.layer != wallsLayerIndex)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, "scene.wallsLayer",
                            "Scene '" + sceneName + "' obstacle Tilemap '" + map.name +
                            "' is not on the Walls layer (obstacles must block pathfinding)."));
                    }
                }
                }

                int mainCameras = 0;
                int enabledMainCameras = 0;
                foreach (Camera sceneCamera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                {
                    if (!sceneCamera.CompareTag("MainCamera"))
                        continue;

                    mainCameras++;
                    if (sceneCamera.isActiveAndEnabled)
                        enabledMainCameras++;
                }

                if (mainCameras == 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "scene.camera",
                        "Scene '" + sceneName + "' has no Camera tagged MainCamera."));
                }
                else if (enabledMainCameras > 1)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "scene.camera.multiple",
                        "Scene '" + sceneName + "' has " + enabledMainCameras + " enabled MainCamera cameras; " +
                        "a static one can render over the follow camera. Untag or disable the extras."));
                }

                if (UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include).Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "scene.eventSystem",
                        "Scene '" + sceneName + "' has no EventSystem; the bootstrap supplies one at runtime, " +
                        "so UI still works, but add one if the scene needs its own UI routing."));
                }

                if (UnityEngine.Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include).Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "scene.inventoryUi",
                        "Scene '" + sceneName + "' has no InventoryUI; the shared Resources/InventoryCanvas.prefab " +
                        "is instantiated at runtime for scenes without one."));
                }
                if (spawnPoints > 0 &&
                    UnityEngine.Object.FindObjectsByType<PlayerControllerBase>(FindObjectsInactive.Include).Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "scene.player",
                        "Scene '" + sceneName + "' has a PlayerSpawnPoint but no PlayerControllerBase; there is nothing for the player to spawn into."));
                }
            }
            catch (Exception e)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning, "scene.open", "Could not scan " + path + ": " + e.Message));
            }
        }
    }

    private static IEnumerable<string> EnumerateScenePaths() =>
        AssetDatabase.FindAssets("t:Scene", SceneFolders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .OrderBy(p => p);

    private static bool AnyOpenSceneDirty()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                return true;
        }
        return false;
    }

    // ── IO helpers ───────────────────────────────────────────────────────────

    private static T ReadJson<T>(string path, List<ValidationIssue> issues) where T : class
    {
        if (!File.Exists(path))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "file.missing", "Missing data file: " + path));
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, "file.invalid", "Could not parse " + path + ": " + e.Message));
            return null;
        }
    }

    private static void WriteReport(List<ValidationIssue> issues)
    {
        var errors = issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
        var warnings = issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Game data validation");
        sb.AppendLine($"errors={errors.Count} warnings={warnings.Count}");
        foreach (ValidationIssue issue in issues.OrderByDescending(i => i.Severity))
            sb.AppendLine("  " + issue);

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Temp");
        File.WriteAllText(ReportPath, sb.ToString());

        string summary = $"Game data validation: {errors.Count} error(s), {warnings.Count} warning(s). See {ReportPath}.";
        if (errors.Count > 0)
            Debug.LogError(summary);
        else if (warnings.Count > 0)
            Debug.LogWarning(summary);
        else
            Debug.Log(summary);
    }

    // ── JSON DTOs (shapes match Assets/StreamingAssets) ───────────────────────

    [Serializable] private sealed class ItemsFile { public int version; public ItemRecord[] items; }
    [Serializable] private sealed class ItemRecord { public string id; }

    [Serializable] private sealed class NpcInventoryFile { public int version; public NpcInventoryEntry[] npcInventories; }
    [Serializable] private sealed class NpcInventoryEntry { public string npcId; public NpcInventoryItem[] items; }
    [Serializable] private sealed class NpcInventoryItem { public string itemId; public int quantity; }

    [Serializable] private sealed class EnemyLootFile { public int version; public EnemyLootEntry[] enemyLoot; }
    [Serializable] private sealed class EnemyLootEntry { public string npcId; public EnemyLootItem[] items; }
    [Serializable] private sealed class EnemyLootItem { public string itemId; public int minQuantity; public int maxQuantity; }

    [Serializable] private sealed class DialogueFile { public int version; public DialogueGraph[] dialogues; }
    [Serializable] private sealed class DialogueGraph { public string dialogueId; public string startNodeId; public DialogueNode[] nodes; }
    [Serializable] private sealed class DialogueNode { public string id; public string nextNodeId; public bool endConversation; public DialogueChoice[] choices; }
    [Serializable] private sealed class DialogueChoice { public string nextNodeId; public bool endConversation; public string questId; public string teleportScene; }
}
