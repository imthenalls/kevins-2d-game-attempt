using System.Collections.Generic;

/// <summary>
/// Plain C# data classes that map 1-to-1 with the JSON quest file format.
/// Deserialized by QuestLoader using Newtonsoft.Json. Not MonoBehaviours.
///
/// Quest graph structure:
///   QuestGraphData      — one quest (questId, startNodeId, list of nodes).
///     QuestNodeData     — one quest stage (objectives to complete, transitions, onEnter actions).
///       QuestObjectiveData  — one thing the player must do (eventType + targetId + requiredCount).
///       QuestTransitionData — an edge in the graph; conditions gate when it fires.
///       QuestActionData     — side effect executed when the node is entered (give item, set fact, etc.).
///
/// Condition types (QuestConditionData.type):
///   ObjectiveComplete — objectiveId must be satisfied.
///   Fact              — WorldStateManager[key] == value.
///   QuestInNode       — another quest is at a specific node.
///   HasItem           — player inventory contains itemId × count.
///
/// Action types (QuestActionData.type):
///   SetFact   — write a key/value pair to WorldStateManager.
///   ClearFlag — remove a flag key from WorldStateManager (HasFlag returns false).
///   ToggleFlag— flip a boolean flag in WorldStateManager.
///   GiveItem  — add itemId × count to the player inventory.
///   RemoveItem— remove itemId × count from the player inventory.
///   StartQuest— activate another quest by questId.
///
/// Unity setup: none — purely data containers.
///   Define quests in JSON files inside Assets/StreamingAssets/quests/.
/// </summary>

public class QuestGraphData
{
    public string questId;
    public string startNodeId;
    public List<QuestNodeData> nodes = new();
}

public class QuestNodeData
{
    public string id;
    public List<QuestObjectiveData> objectives = new();
    public List<QuestTransitionData> transitions = new();
    public List<QuestActionData> onEnterActions = new();
}

public class QuestObjectiveData
{
    public string id;
    public string eventType;
    public string targetId;
    public int requiredCount = 1;
}

public class QuestTransitionData
{
    public string targetNodeId;
    public bool automatic;
    public List<QuestConditionData> conditions = new();
}

/// <summary>
/// Flat struct holding all possible fields for any condition type.
/// The "type" discriminator field selects which fields are used.
///
///   ObjectiveComplete : objectiveId
///   Fact              : key, value
///   QuestInNode       : questId, nodeId
///   HasItem           : itemId, count
/// </summary>
public class QuestConditionData
{
    public string type;
    public string objectiveId;
    public string key;
    public string value;
    public string questId;
    public string nodeId;
    public string itemId;
    public int count = 1;
}

/// <summary>
/// Flat struct holding all possible fields for any action type.
/// The "type" discriminator field selects which fields are used.
///
///   SetFact    : key, value
///   GiveItem   : itemId, count
///   RemoveItem : itemId, count
///   StartQuest : questId
/// </summary>
public class QuestActionData
{
    public string type;
    public string key;
    public string value;
    public string itemId;
    public int count = 1;
    public string questId;
}
