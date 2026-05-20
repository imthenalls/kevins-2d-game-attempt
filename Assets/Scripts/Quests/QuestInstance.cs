using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime state for one active quest. Wraps a QuestGraphData and tracks
/// which nodes are currently active and how far each objective has progressed.
///
/// Supports multiple simultaneously active nodes (parallel objectives).
/// Advancing through the graph is driven by QuestManager via OnEvent() and TryAdvance().
/// </summary>
public class QuestInstance
{
    public readonly QuestGraphData Graph;

    private readonly HashSet<string> _activeNodeIds = new();
    private readonly Dictionary<string, int> _objectiveCounts = new();
    private readonly Dictionary<string, QuestNodeData> _nodeMap = new();

    public QuestInstance(QuestGraphData graph)
    {
        Graph = graph;
        foreach (var node in graph.nodes)
            _nodeMap[node.id] = node;

        EnterNode(graph.startNodeId);
    }

    // Private constructor used by FromSave — builds the node map but does NOT
    // call EnterNode so that onEnterActions are not re-fired on load.
    private QuestInstance(QuestGraphData graph, bool _restoreMode)
    {
        Graph = graph;
        foreach (var node in graph.nodes)
            _nodeMap[node.id] = node;
    }

    /// <summary>
    /// Reconstructs a QuestInstance from saved state without firing onEnterActions.
    /// Called by QuestManager.LoadSaveData.
    /// </summary>
    public static QuestInstance FromSave(
        QuestGraphData graph,
        System.Collections.Generic.List<string> nodeIds,
        System.Collections.Generic.Dictionary<string, int> counts)
    {
        var inst = new QuestInstance(graph, _restoreMode: true);
        foreach (var id in nodeIds)
            inst._activeNodeIds.Add(id);
        foreach (var kv in counts)
            inst._objectiveCounts[kv.Key] = kv.Value;
        return inst;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>True if this instance is currently sitting at the given node.</summary>
    public bool IsInNode(string nodeId) => _activeNodeIds.Contains(nodeId);

    /// <summary>
    /// True when the named objective has reached its required count.
    /// Searches across all currently active nodes.
    /// </summary>
    public bool IsObjectiveComplete(string objectiveId)
    {
        foreach (var nodeId in _activeNodeIds)
        {
            if (!_nodeMap.TryGetValue(nodeId, out var node)) continue;
            foreach (var obj in node.objectives)
            {
                if (obj.id != objectiveId) continue;
                _objectiveCounts.TryGetValue(objectiveId, out int current);
                return current >= obj.requiredCount;
            }
        }
        return false;
    }

    /// <summary>Read-only view of active node ids (for save/load).</summary>
    public IReadOnlyCollection<string> ActiveNodeIds => _activeNodeIds;

    /// <summary>Read-only view of objective counts (for save/load).</summary>
    public IReadOnlyDictionary<string, int> ObjectiveCounts => _objectiveCounts;

    // -------------------------------------------------------------------------
    // Event handling
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by QuestManager when a game event fires.
    /// Increments matching objective counters then tries to advance the graph.
    /// </summary>
    public void OnEvent(string eventType, string targetId, int amount)
    {
        // Snapshot so EnterNode modifications to _activeNodeIds don't break iteration
        var snapshot = new List<string>(_activeNodeIds);

        foreach (var nodeId in snapshot)
        {
            if (!_nodeMap.TryGetValue(nodeId, out var node)) continue;

            foreach (var obj in node.objectives)
            {
                if (obj.eventType != eventType) continue;
                if (!MatchesTarget(obj.targetId, targetId)) continue;

                _objectiveCounts.TryGetValue(obj.id, out int current);
                if (current < obj.requiredCount)
                    _objectiveCounts[obj.id] = Mathf.Min(current + amount, obj.requiredCount);
            }
        }

        TryAdvance();
    }

    // -------------------------------------------------------------------------
    // Graph traversal
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates all transitions on active nodes and advances any that pass.
    /// Loops until no further automatic advancement is possible in one tick.
    /// Called by QuestManager every Update for automatic transitions, and
    /// also after every OnEvent.
    /// </summary>
    public void TryAdvance()
    {
        bool anyEntered = true;

        while (anyEntered)
        {
            anyEntered = false;
            var snapshot = new List<string>(_activeNodeIds);

            foreach (var nodeId in snapshot)
            {
                if (!_activeNodeIds.Contains(nodeId)) continue; // already left this node
                if (!_nodeMap.TryGetValue(nodeId, out var node)) continue;

                foreach (var transition in node.transitions)
                {
                    if (!AllConditionsMet(transition)) continue;

                    _activeNodeIds.Remove(nodeId);
                    EnterNode(transition.targetNodeId);
                    anyEntered = true;
                    break; // re-evaluate the snapshot on the next while pass
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private void EnterNode(string nodeId)
    {
        if (!_nodeMap.TryGetValue(nodeId, out var node))
        {
            Debug.LogWarning($"[QuestInstance:{Graph.questId}] Node '{nodeId}' not found.");
            return;
        }

        _activeNodeIds.Add(nodeId);

        foreach (var actionData in node.onEnterActions)
        {
            var action = QuestLoader.BuildAction(actionData);
            action?.Execute();
        }

        if (node.transitions.Count == 0)
            Debug.Log($"[Quest:{Graph.questId}] Reached terminal node '{nodeId}'.");
    }

    private bool AllConditionsMet(QuestTransitionData transition)
    {
        foreach (var condData in transition.conditions)
        {
            var condition = QuestLoader.BuildCondition(condData);
            if (condition == null || !condition.Evaluate(this))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Matches an objective's targetId pattern against the event's targetId.
    /// A null/empty pattern or "*" matches any value.
    /// </summary>
    private static bool MatchesTarget(string pattern, string value)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*") return true;
        return pattern == value;
    }
}
