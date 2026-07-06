using UnityEngine;

/// <summary>
/// Evaluates to true or false given the current quest instance context.
/// All conditions on a transition must evaluate to true for the transition to fire.
/// To express OR logic, use multiple transitions that point to the same target node.
///
/// Built-in implementations (created by QuestLoader from the "type" field in JSON):
///   ObjectiveCompleteCondition — { "type": "ObjectiveComplete", "objectiveId": "..." }
///   FactCondition              — { "type": "Fact", "key": "...", "value": "..." }
///   QuestInNodeCondition       — { "type": "QuestInNode", "questId": "...", "nodeId": "..." }
///   HasItemCondition           — { "type": "HasItem", "itemId": "...", "count": 1 }
///
/// Unity setup: none — conditions are pure C# objects, not MonoBehaviours.
///   To add a custom condition: implement ICondition and register the type string
///   in QuestLoader's condition factory method.
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
// True when WorldStateManager[key].ToString() == value (case-insensitive).
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
        if (WorldStateManager.Instance == null) return false;
        object fact = WorldStateManager.Instance.GetFact(_key);
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
