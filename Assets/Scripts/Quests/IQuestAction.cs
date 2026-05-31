using UnityEngine;

/// <summary>
/// A side effect that executes once when a quest node is entered.
/// Built-in implementations (created by QuestLoader from the "type" field in JSON):
///   SetFactAction    — { "type": "SetFact",    "key": "...", "value": "..." }
///   GiveItemAction   — { "type": "GiveItem",   "itemId": "...", "count": 1 }
///   RemoveItemAction — { "type": "RemoveItem", "itemId": "...", "count": 1 }
///   StartQuestAction — { "type": "StartQuest", "questId": "..." }
///
/// Unity setup: none — actions are pure C# objects, not MonoBehaviours.
///   To add a custom action: implement IQuestAction and register the type string
///   in QuestLoader's action factory method.
/// </summary>
public interface IQuestAction
{
    void Execute();
}

// ---------------------------------------------------------------------------
// SetFact
// JSON: { "type": "SetFact", "key": "banditKingDead", "value": "True" }
// Writes a string value into WorldStateDB. Other quests/conditions can read it.
// ---------------------------------------------------------------------------
public class SetFactAction : IQuestAction
{
    private readonly string _key;
    private readonly string _value;

    public SetFactAction(string key, string value)
    {
        _key = key;
        _value = value;
    }

    public void Execute()
    {
        if (WorldStateDB.Instance == null)
        {
            Debug.LogWarning("[SetFactAction] WorldStateDB not found in scene.");
            return;
        }
        WorldStateDB.Instance.SetFact(_key, _value);
    }
}

// ---------------------------------------------------------------------------
// GiveItem
// JSON: { "type": "GiveItem", "itemId": "reward_coin_pouch", "count": 1 }
// Adds items to the player inventory. itemId must be an ItemData asset name
// inside a Resources folder (e.g. Assets/Resources/reward_coin_pouch.asset).
// ---------------------------------------------------------------------------
public class GiveItemAction : IQuestAction
{
    private readonly string _itemId;
    private readonly int _count;

    public GiveItemAction(string itemId, int count)
    {
        _itemId = itemId;
        _count = count;
    }

    public void Execute()
    {
        var model = InventoryUI.Model;
        if (model == null)
        {
            Debug.LogWarning("[GiveItemAction] InventoryUI.Model is null.");
            return;
        }

        var item = Resources.Load<ItemData>(_itemId);
        if (item == null)
        {
            Debug.LogWarning($"[GiveItemAction] ItemData not found at Resources/{_itemId}");
            return;
        }

        int leftover = model.AddItem(item, _count);
        if (leftover > 0)
            Debug.LogWarning($"[GiveItemAction] Inventory full; {leftover}x {item.itemName} could not be added.");
    }
}

// ---------------------------------------------------------------------------
// RemoveItem
// JSON: { "type": "RemoveItem", "itemId": "evidence_letter", "count": 1 }
// Removes items from the player inventory.
// ---------------------------------------------------------------------------
public class RemoveItemAction : IQuestAction
{
    private readonly string _itemId;
    private readonly int _count;

    public RemoveItemAction(string itemId, int count)
    {
        _itemId = itemId;
        _count = count;
    }

    public void Execute()
    {
        var model = InventoryUI.Model;
        if (model == null)
        {
            Debug.LogWarning("[RemoveItemAction] InventoryUI.Model is null.");
            return;
        }

        var item = Resources.Load<ItemData>(_itemId);
        if (item == null)
        {
            Debug.LogWarning($"[RemoveItemAction] ItemData not found at Resources/{_itemId}");
            return;
        }

        if (!model.RemoveItem(item, _count))
            Debug.LogWarning($"[RemoveItemAction] Could not remove {_count}x {item.itemName}; not enough in inventory.");
    }
}

// ---------------------------------------------------------------------------
// StartQuest
// JSON: { "type": "StartQuest", "questId": "follow_up_quest" }
// Activates another quest graph. Safe to call even if already active (logs warning).
// ---------------------------------------------------------------------------
public class StartQuestAction : IQuestAction
{
    private readonly string _questId;

    public StartQuestAction(string questId) => _questId = questId;

    public void Execute()
    {
        if (QuestManager.Instance == null)
        {
            Debug.LogWarning("[StartQuestAction] QuestManager not found in scene.");
            return;
        }
        QuestManager.Instance.StartQuest(_questId);
    }
}
