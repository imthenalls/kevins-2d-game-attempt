using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central singleton that owns all active quest instances.
///
/// Responsibilities:
///   - Load all quest graphs from StreamingAssets/quests/ on Awake
///   - Subscribe to QuestEventBus and forward events to active instances
///   - Tick automatic transitions every Update
///   - Expose StartQuest() for dialogue/triggers to call
///   - Expose query methods used by QuestInNode conditions
///
/// Scene setup: add to one persistent GameObject in your bootstrap/first scene.
/// Requires WorldStateManager on the same or another DontDestroyOnLoad object.
/// </summary>
[DisallowMultipleComponent]
public class QuestManager : MonoBehaviour
{
    private static QuestManager _instance;
    public static QuestManager Instance => _instance;

    private Dictionary<string, QuestGraphData> _allGraphs = new();
    private readonly List<QuestInstance> _activeQuests = new();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _allGraphs = QuestLoader.LoadAll();
        Debug.Log($"[QuestManager] Loaded {_allGraphs.Count} quest graph(s).");
    }

    private void OnEnable()  => QuestEventBus.OnEvent += HandleEvent;
    private void OnDisable() => QuestEventBus.OnEvent -= HandleEvent;

    private void Update()
    {
        // Drive automatic transitions each frame
        foreach (var quest in _activeQuests)
            quest.TryAdvance();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Activates a quest by id. Does nothing if already active.
    /// Call from dialogue, cutscene triggers, or other StartQuestActions.
    /// </summary>
    public void StartQuest(string questId)
    {
        if (!_allGraphs.TryGetValue(questId, out var graph))
        {
            Debug.LogError($"[QuestManager] Quest '{questId}' not found. Check the JSON filename and questId field.");
            return;
        }

        if (IsQuestActive(questId))
        {
            Debug.LogWarning($"[QuestManager] Quest '{questId}' is already active.");
            return;
        }

        _activeQuests.Add(new QuestInstance(graph));
        Debug.Log($"[QuestManager] Started quest '{questId}'.");
    }

    /// <summary>True if a quest with this id is currently active.</summary>
    public bool IsQuestActive(string questId)
    {
        foreach (var q in _activeQuests)
            if (q.Graph.questId == questId) return true;
        return false;
    }

    /// <summary>
    /// True if an active quest instance is currently sitting at the given node.
    /// Used by QuestInNodeCondition for cross-quest dependencies.
    /// </summary>
    public bool IsQuestInNode(string questId, string nodeId)
    {
        foreach (var q in _activeQuests)
            if (q.Graph.questId == questId)
                return q.IsInNode(nodeId);
        return false;
    }

    // -------------------------------------------------------------------------
    // Save / load support
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class QuestSaveEntry
    {
        public string questId;
        public List<string> activeNodeIds;
        public List<ObjectiveCountEntry> objectiveCounts;
    }

    [System.Serializable]
    public class ObjectiveCountEntry
    {
        public string objectiveId;
        public int count;
    }

    /// <summary>
    /// Restores active quest instances from a save file entry list.
    /// Uses QuestInstance.FromSave so onEnterActions are NOT re-fired.
    /// Called by SaveManager on load.
    /// </summary>
    public void LoadSaveData(List<QuestSaveEntry> entries)
    {
        _activeQuests.Clear();
        foreach (var entry in entries)
        {
            if (!_allGraphs.TryGetValue(entry.questId, out var graph))
            {
                Debug.LogWarning($"[QuestManager] Quest '{entry.questId}' not found during load — skipping.");
                continue;
            }

            var counts = new Dictionary<string, int>();
            foreach (var oc in entry.objectiveCounts)
                counts[oc.objectiveId] = oc.count;

            _activeQuests.Add(QuestInstance.FromSave(graph, entry.activeNodeIds, counts));
        }
        Debug.Log($"[QuestManager] Restored {_activeQuests.Count} quest(s) from save.");
    }

    /// <summary>Returns serializable save data for all active quests.</summary>
    public List<QuestSaveEntry> GetSaveData()
    {
        var entries = new List<QuestSaveEntry>();
        foreach (var q in _activeQuests)
        {
            var entry = new QuestSaveEntry
            {
                questId = q.Graph.questId,
                activeNodeIds = new List<string>(q.ActiveNodeIds),
                objectiveCounts = new List<ObjectiveCountEntry>(),
            };
            foreach (var kv in q.ObjectiveCounts)
                entry.objectiveCounts.Add(new ObjectiveCountEntry { objectiveId = kv.Key, count = kv.Value });
            entries.Add(entry);
        }
        return entries;
    }

    // -------------------------------------------------------------------------
    // Private
    // -------------------------------------------------------------------------

    private void HandleEvent(string eventType, string targetId, int amount)
    {
        foreach (var quest in _activeQuests)
            quest.OnEvent(eventType, targetId, amount);
    }
}
