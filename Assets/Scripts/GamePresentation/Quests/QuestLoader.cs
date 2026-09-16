using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Loads QuestGraphData from JSON files in StreamingAssets/quests/ and
/// constructs concrete ICondition and IQuestAction instances from the
/// "type" discriminator field in each condition/action entry.
/// </summary>
public static class QuestLoader
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
    };

    // -------------------------------------------------------------------------
    // Loading
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads every *.json file from StreamingAssets/quests/ and returns a
    /// dictionary keyed by questId.
    /// </summary>
    public static Dictionary<string, QuestGraphData> LoadAll()
    {
        var result = new Dictionary<string, QuestGraphData>();
        string dir = Path.Combine(Application.streamingAssetsPath, "quests");

        if (!Directory.Exists(dir))
        {
            Debug.LogWarning($"[QuestLoader] Quests directory not found: {dir}");
            return result;
        }

        foreach (string file in Directory.GetFiles(dir, "*.json"))
        {
            var graph = Load(file);
            if (graph == null) continue;

            if (string.IsNullOrEmpty(graph.questId))
            {
                Debug.LogError($"[QuestLoader] Quest file missing questId: {file}");
                continue;
            }

            if (result.ContainsKey(graph.questId))
                Debug.LogWarning($"[QuestLoader] Duplicate questId '{graph.questId}' in {file}; skipping.");
            else
                result[graph.questId] = graph;
        }

        return result;
    }

    /// <summary>Loads and deserializes a single quest JSON file.</summary>
    public static QuestGraphData Load(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<QuestGraphData>(json, Settings);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[QuestLoader] Failed to load '{path}': {ex.Message}");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // Condition factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constructs the correct ICondition from a QuestConditionData's "type" field.
    /// Returns null and logs an error for unknown types.
    /// </summary>
    public static ICondition BuildCondition(QuestConditionData data)
    {
        if (data == null) return null;

        return data.type switch
        {
            "ObjectiveComplete" => new ObjectiveCompleteCondition(data.objectiveId),
            "Fact"              => new FactCondition(data.key, data.value),
            "QuestInNode"       => new QuestInNodeCondition(data.questId, data.nodeId),
            "HasItem"           => new HasItemCondition(data.itemId, data.count),
            _                   => UnknownType<ICondition>(data.type, "condition"),
        };
    }

    // -------------------------------------------------------------------------
    // Action factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Constructs the correct IQuestAction from a QuestActionData's "type" field.
    /// Returns null and logs an error for unknown types.
    /// </summary>
    public static IQuestAction BuildAction(QuestActionData data)
    {
        if (data == null) return null;

        return data.type switch
        {
            "SetFact"    => new SetFactAction(data.key, data.value),
            "ClearFlag"  => new ClearFlagAction(data.key),
            "ToggleFlag" => new ToggleFlagAction(data.key),
            "GiveItem"   => new GiveItemAction(data.itemId, data.count),
            "RemoveItem" => new RemoveItemAction(data.itemId, data.count),
            "StartQuest" => new StartQuestAction(data.questId),
            _            => UnknownType<IQuestAction>(data.type, "action"),
        };
    }

    private static T UnknownType<T>(string type, string category) where T : class
    {
        Debug.LogError($"[QuestLoader] Unknown {category} type: '{type}'");
        return null;
    }
}
