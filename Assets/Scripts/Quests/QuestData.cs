using System.Collections.Generic;

/// <summary>
/// Raw data classes that map 1-to-1 with the JSON quest files.
/// Deserialized by QuestLoader using Newtonsoft.Json.
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
