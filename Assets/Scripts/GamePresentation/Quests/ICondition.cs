using Game.Core;
using UnityEngine;

// The interface now lives in Game.Data (Game.Core). Implementations below stay here.
// ObjectiveComplete moved to Game.Data (Game.Core).

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

        var item = ItemDatabase.Instance?.Get(_itemId) ?? Resources.Load<ItemData>(_itemId);
        if (item == null)
        {
            Debug.LogWarning($"[HasItemCondition] ItemData not found at Resources/{_itemId}");
            return false;
        }

        return (item.flags & ItemFlags.KeyItem) != 0
            ? PlayerKeyring.GetOrCreate().HasKey(item.itemId, _count)
            : model.CountItem(item) >= _count;
    }
}
