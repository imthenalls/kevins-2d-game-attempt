using UnityEngine;

/// <summary>
/// Evaluates to true or false given the current quest instance context.
/// All conditions on a transition must evaluate to true for the transition to fire.
/// To express OR logic, use multiple transitions pointing to the same target node.
/// </summary>
public interface ICondition
{
    bool Evaluate(QuestInstance ctx);
}

// ---------------------------------------------------------------------------
// ObjectiveComplete
// JSON: { "type": "ObjectiveComplete", "objectiveId": "obj_talk" }
// True when the named objective on the current node has reached its required count.
// ---------------------------------------------------------------------------
public class ObjectiveCompleteCondition : ICondition
{
    private readonly string _objectiveId;

    public ObjectiveCompleteCondition(string objectiveId) => _objectiveId = objectiveId;

    public bool Evaluate(QuestInstance ctx) => ctx.IsObjectiveComplete(_objectiveId);
}

// ---------------------------------------------------------------------------
// Fact
// JSON: { "type": "Fact", "key": "sheriffTrusted", "value": "True" }
// True when WorldStateDB[key].ToString() == value (case-insensitive).
// ---------------------------------------------------------------------------
public class FactCondition : ICondition
{
    private readonly string _key;
    private readonly string _value;

    public FactCondition(string key, string value)
    {
        _key = key;
        _value = value;
    }

    public bool Evaluate(QuestInstance ctx)
    {
        if (WorldStateDB.Instance == null) return false;
        object fact = WorldStateDB.Instance.GetFact(_key);
        if (fact == null) return false;
        return string.Equals(fact.ToString(), _value, System.StringComparison.OrdinalIgnoreCase);
    }
}

// ---------------------------------------------------------------------------
// QuestInNode
// JSON: { "type": "QuestInNode", "questId": "side_quest_a", "nodeId": "completed" }
// True when another active quest is currently sitting at the specified node.
// ---------------------------------------------------------------------------
public class QuestInNodeCondition : ICondition
{
    private readonly string _questId;
    private readonly string _nodeId;

    public QuestInNodeCondition(string questId, string nodeId)
    {
        _questId = questId;
        _nodeId = nodeId;
    }

    public bool Evaluate(QuestInstance ctx)
    {
        if (QuestManager.Instance == null) return false;
        return QuestManager.Instance.IsQuestInNode(_questId, _nodeId);
    }
}

// ---------------------------------------------------------------------------
// HasItem
// JSON: { "type": "HasItem", "itemId": "evidence_letter", "count": 1 }
// True when the player inventory holds at least count of the item.
// itemId must match the ItemData asset name inside a Resources folder.
// ---------------------------------------------------------------------------
public class HasItemCondition : ICondition
{
    private readonly string _itemId;
    private readonly int _count;

    public HasItemCondition(string itemId, int count)
    {
        _itemId = itemId;
        _count = count;
    }

    public bool Evaluate(QuestInstance ctx)
    {
        var model = InventoryUI.Model;
        if (model == null) return false;

        var item = Resources.Load<ItemData>(_itemId);
        if (item == null)
        {
            Debug.LogWarning($"[HasItemCondition] ItemData not found at Resources/{_itemId}");
            return false;
        }

        return model.CountItem(item) >= _count;
    }
}
