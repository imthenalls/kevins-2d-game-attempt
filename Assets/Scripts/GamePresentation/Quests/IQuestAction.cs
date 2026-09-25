using Game.Core;
using UnityEngine;

// The interface now lives in Game.Data (Game.Core). Implementations below stay here.
// ---------------------------------------------------------------------------
// SetFact
// JSON: { "type": "SetFact", "key": "banditKingDead", "value": "True" }
// Writes a string value into WorldStateManager. Other quests/conditions can read it.
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
        if (WorldStateManager.Instance == null)
        {
            Debug.LogWarning("[SetFactAction] WorldStateManager not found in scene.");
            return;
        }
        WorldStateManager.Instance.SetFact(_key, _value);
    }
}

// ---------------------------------------------------------------------------
// GiveItem
// JSON: { "type": "GiveItem", "itemId": "reward_coin_pouch", "count": 1 }
// Adds items to the player inventory. itemId must be an ItemData asset name
// inside a Resources folder (e.g. Assets/Resources/reward_coin_pouch.asset).
// If the inventory cannot hold everything, the remainder is retained as a pending reward.
// ---------------------------------------------------------------------------
public class GiveItemAction : IQuestAction
{
    private readonly string _itemId;
    private readonly int _count;
    private readonly string _questId;

    public GiveItemAction(string itemId, int count, string questId = "")
    {
        _itemId = itemId;
        _count = count;
        _questId = questId;
    }

    public void Execute()
    {
        // Route through the pending-reward manager so a full inventory retains the reward instead of
        // silently discarding it.
        if (PendingRewardManager.Instance != null)
        {
            PendingRewardManager.Instance.GrantReward(_questId, _itemId, _count);
            return;
        }

        // Fallback for minimal scenes without the manager: deliver directly.
        var model = InventoryUI.Model;
        if (model == null)
        {
            Debug.LogWarning("[GiveItemAction] InventoryUI.Model is null.");
            return;
        }

        var item = ItemDatabase.Instance?.Get(_itemId) ?? Resources.Load<ItemData>(_itemId);
        if (item == null)
        {
            Debug.LogWarning($"[GiveItemAction] ItemData not found at Resources/{_itemId}");
            return;
        }

        int taken = InventoryHelper.GiveItem(item, _count);
        int leftover = _count - taken;
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

        var item = ItemDatabase.Instance?.Get(_itemId) ?? Resources.Load<ItemData>(_itemId);
        if (item == null)
        {
            Debug.LogWarning($"[RemoveItemAction] ItemData not found at Resources/{_itemId}");
            return;
        }

        bool removed = (item.flags & ItemFlags.KeyItem) != 0
            ? PlayerKeyring.GetOrCreate().RemoveKey(item.itemId, _count)
            : model.RemoveItem(item, _count);
        if (!removed)
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

// ---------------------------------------------------------------------------
// ClaimRewards
// JSON: { "type": "ClaimRewards" }
// Retries delivery of any pending (undelivered) quest rewards.
// ---------------------------------------------------------------------------
public class ClaimRewardsAction : IQuestAction
{
    public void Execute()
    {
        if (PendingRewardManager.Instance != null)
            PendingRewardManager.Instance.ClaimPending();
    }
}

// ---------------------------------------------------------------------------
// GrantMana
// JSON: { "type": "GrantMana", "amount": 100 }
// Grants mana (currency) to the player's wallet as a quest reward.
// ---------------------------------------------------------------------------
public class GrantManaAction : IQuestAction
{
    private readonly int _amount;
    private readonly string _questId;

    public GrantManaAction(int amount, string questId = "")
    {
        _amount = amount;
        _questId = questId;
    }

    public void Execute()
    {
        if (_amount <= 0)
            return;

        var player = UnityEngine.Object.FindAnyObjectByType<PlayerControllerBase>();
        Wallet wallet = player != null ? player.ManaWallet : null;
        if (wallet == null)
        {
            Debug.LogWarning("[GrantManaAction] Player wallet not found.");
            return;
        }

        if (!wallet.Add(_amount, "Quest reward", _questId))
            Debug.LogWarning($"[GrantManaAction] Could not grant {_amount} mana (wallet full).");
    }
}

// ---------------------------------------------------------------------------
// ClearFlag
// JSON: { "type": "ClearFlag", "key": "Dungeon.TorchLit" }
// Removes a world state flag so HasFlag returns false.
// ---------------------------------------------------------------------------
public class ClearFlagAction : IQuestAction
{
    private readonly string _key;
    public ClearFlagAction(string key) => _key = key;

    public void Execute()
    {
        if (WorldStateManager.Instance == null)
        {
            Debug.LogWarning("[ClearFlagAction] WorldStateManager not found in scene.");
            return;
        }
        WorldStateManager.Instance.ClearFlag(_key);
    }
}

// ---------------------------------------------------------------------------
// ToggleFlag
// JSON: { "type": "ToggleFlag", "key": "Village.MarketOpen" }
// Sets the flag if absent, clears it if present.
// ---------------------------------------------------------------------------
public class ToggleFlagAction : IQuestAction
{
    private readonly string _key;
    public ToggleFlagAction(string key) => _key = key;

    public void Execute()
    {
        if (WorldStateManager.Instance == null)
        {
            Debug.LogWarning("[ToggleFlagAction] WorldStateManager not found in scene.");
            return;
        }
        WorldStateManager.Instance.ToggleFlag(_key);
    }
}
