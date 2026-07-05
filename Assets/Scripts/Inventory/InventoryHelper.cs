using UnityEngine;

/// <summary>
/// Static utility for giving items to the player's inventory from world systems.
/// Centralises the three-step pattern: AddItem → CharacterStatistics → QuestEventBus.
///
/// Unity setup: none — static class, no GameObject required.
///
/// Usage:
///   int taken = InventoryHelper.GiveItem(item, quantity, recipientGameObject);
///   // taken == 0 means the inventory was full and nothing was added.
///
/// The recipient argument is optional. When provided, CharacterStatistics.RecordItemGathered()
/// is called on it if the component is present. Pass null to skip statistics tracking
/// (e.g. internal systems that should not affect player stats).
/// </summary>
public static class InventoryHelper
{
    /// <summary>
    /// Adds <paramref name="quantity"/> of <paramref name="item"/> to the player's inventory,
    /// notifies CharacterStatistics on <paramref name="recipient"/>, and raises QuestEventBus.
    /// Returns the number of items actually added (less than requested if the inventory was full).
    /// </summary>
    public static int GiveItem(ItemData item, int quantity, GameObject recipient = null)
    {
        if (item == null)
        {
            Debug.LogWarning("[InventoryHelper] GiveItem called with a null ItemData.");
            return 0;
        }

        var inv = InventoryUI.Model;
        if (inv == null) return 0;

        int leftover = inv.AddItem(item, quantity);
        int taken    = quantity - leftover;

        if (taken <= 0)
        {
            Debug.Log($"[InventoryHelper] Inventory full — could not add '{item.itemName}'.");
            return 0;
        }

        if (recipient != null && recipient.TryGetComponent<CharacterStatistics>(out var stats))
            stats.RecordItemGathered(taken);

        QuestEventBus.Raise("ItemCollected", item.itemId, taken);

        return taken;
    }
}
