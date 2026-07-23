using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Static runtime registry of all dialogue graphs loaded into memory.
/// Graphs come from two sources:
///   1. StreamingAssets/dialogues.json — loaded lazily on the first TryGetDialogue call.
///   2. DialogueGraphAsset ScriptableObjects — registered via RegisterAsset(),
///      which NpcDialogue calls automatically in Awake.
///
/// Unity setup: no MonoBehaviour needed — completely static.
///   • JSON dialogues: place dialogues.json in Assets/StreamingAssets/.
///   • Asset dialogues: assign a DialogueGraphAsset to NpcDialogue; registration is automatic.
///   Both sources can coexist. JSON is loaded lazily; assets register eagerly on scene load.
/// </summary>
public static class DialogueDatabase
{
    private const string DialogueDatabaseFileName = "dialogues.json";

    private static readonly Dictionary<string, DialogueGraphDefinition> dialoguesById = new Dictionary<string, DialogueGraphDefinition>(StringComparer.OrdinalIgnoreCase);
    private static bool hasLoadedFromJson;

    public static void RegisterAsset(DialogueGraphAsset asset)
    {
        if (asset == null || asset.Graph == null)
        {
            return;
        }

        RegisterGraph(asset.Graph);
    }

    public static bool TryGetDialogue(string dialogueId, out DialogueGraphDefinition graph)
    {
        if (string.IsNullOrWhiteSpace(dialogueId))
        {
            graph = null;
            return false;
        }

        EnsureJsonLoaded();
        return dialoguesById.TryGetValue(dialogueId, out graph);
    }

    private static void EnsureJsonLoaded()
    {
        if (hasLoadedFromJson)
        {
            return;
        }

        hasLoadedFromJson = true;

        string jsonPath = Path.Combine(Application.streamingAssetsPath, DialogueDatabaseFileName);
        if (!File.Exists(jsonPath))
        {
            return;
        }

        string json = File.ReadAllText(jsonPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        DialogueDatabaseJson database = JsonUtility.FromJson<DialogueDatabaseJson>(json);
        if (database == null || database.dialogues == null)
        {
            return;
        }

        for (int i = 0; i < database.dialogues.Count; i++)
        {
            RegisterGraph(database.dialogues[i]);
        }
    }

    private static void RegisterGraph(DialogueGraphDefinition graph)
    {
        if (graph == null || string.IsNullOrWhiteSpace(graph.dialogueId))
        {
            return;
        }

        dialoguesById[graph.dialogueId] = graph;
    }
}
