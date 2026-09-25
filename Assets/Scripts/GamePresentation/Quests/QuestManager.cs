using Game.Core;
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
///   - Expose TryChooseTransition() for dialogue/player-selected manual branches
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

    // Starts requested while an event or update tick is still processing are queued and flushed
    // afterwards, so a quest starting another quest cannot modify the active list mid-iteration.
    private readonly List<string> _pendingStarts = new();
    private int _processingDepth;

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
        BeginProcessing();
        try
        {
            // Drive automatic transitions each frame. Starts requested during TryAdvance are queued
            // (not added to the active list), so this list is not mutated while enumerating.
            foreach (var quest in _activeQuests)
                quest.TryAdvance();
        }
        finally
        {
            EndProcessing();
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Activates a quest by id. Does nothing if already active (or already queued to start).
    /// Call from dialogue, cutscene triggers, or other StartQuestActions.
    /// </summary>
    public void StartQuest(string questId)
    {
        if (!_allGraphs.TryGetValue(questId, out var graph))
        {
            Debug.LogError($"[QuestManager] Quest '{questId}' not found. Check the JSON filename and questId field.");
            return;
        }

        if (IsQuestActive(questId) || _pendingStarts.Contains(questId))
        {
            Debug.LogWarning($"[QuestManager] Quest '{questId}' is already active.");
            return;
        }

        if (_processingDepth > 0)
        {
            _pendingStarts.Add(questId);
            return;
        }

        ActivateQuest(questId, graph);
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

    /// <summary>
    /// Choose an eligible manual transition by target node. Use the source-node overload when
    /// a quest can have parallel active nodes with the same target.
    /// </summary>
    public bool TryChooseTransition(string questId, string targetNodeId)
    {
        foreach (var quest in _activeQuests)
        {
            if (quest.Graph.questId == questId)
                return quest.TryChooseTransition(targetNodeId);
        }
        return false;
    }

    /// <summary>Choose an eligible manual transition from a specific active source node.</summary>
    public bool TryChooseTransition(string questId, string sourceNodeId, string targetNodeId)
    {
        foreach (var quest in _activeQuests)
        {
            if (quest.Graph.questId == questId)
                return quest.TryChooseTransition(sourceNodeId, targetNodeId);
        }
        return false;
    }

    // -------------------------------------------------------------------------
    // Save / load support
    // -------------------------------------------------------------------------

    /// <summary>
    /// Restores active quest instances from a save file entry list.
    /// Uses QuestInstance.FromSave so onEnterActions are NOT re-fired.
    /// Called by SaveManager on load.
    /// </summary>
    public void LoadSaveData(List<QuestSaveEntry> entries)
    {
        _activeQuests.Clear();
        _pendingStarts.Clear();
        _processingDepth = 0;
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

    // Registers the instance BEFORE running its initial actions, so a recursive StartQuest from the
    // initial actions sees this quest as active and does not create a duplicate (or loop forever).
    private void ActivateQuest(string questId, QuestGraphData graph)
    {
        var instance = QuestInstance.Deferred(graph);
        _activeQuests.Add(instance);
        instance.Begin();
        Debug.Log($"[QuestManager] Started quest '{questId}'.");
    }

    private void BeginProcessing() => _processingDepth++;

    private void EndProcessing()
    {
        _processingDepth--;
        if (_processingDepth == 0)
            FlushPendingStarts();
    }

    private void FlushPendingStarts()
    {
        while (_pendingStarts.Count > 0)
        {
            var pending = new List<string>(_pendingStarts);
            _pendingStarts.Clear();

            foreach (var questId in pending)
            {
                if (IsQuestActive(questId))
                    continue;
                if (_allGraphs.TryGetValue(questId, out var graph))
                    ActivateQuest(questId, graph);
            }
        }
    }

    private void HandleEvent(string eventType, string targetId, int amount)
    {
        BeginProcessing();
        try
        {
            // Snapshot so a quest started during this event does not receive it (it is queued and
            // flushed after the loop), and so the list is never mutated mid-iteration.
            var snapshot = new List<QuestInstance>(_activeQuests);
            foreach (var quest in snapshot)
                quest.OnEvent(eventType, targetId, amount);
        }
        finally
        {
            EndProcessing();
        }
    }
}
