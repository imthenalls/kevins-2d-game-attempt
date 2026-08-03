using UnityEngine;

/// <summary>
/// Moves owned items between inventories without currency and without allowing partial,
/// duplicated, or lost transfers.
///
/// Unity setup: none. This is a static service used by NPC gifts and other ownership moves.
///
/// Runtime API:
///   GiftTransferResult result = InventoryTransferService.TryGive(
///       npc.Inventory, InventoryUI.Model, item, quantity, playerGameObject);
/// </summary>
public static class InventoryTransferService
{
    /// <summary>
    /// Atomically removes an exact quantity from the source and adds it to the destination.
    /// A failed transfer leaves both inventories unchanged.
    /// </summary>
    public static GiftTransferResult TryGive(
        InventoryModel source,
        InventoryModel destination,
        ItemData item,
        int quantity,
        GameObject recipient = null)
    {
        if (source == null || destination == null)
            return GiftTransferResult.Failed("Both source and destination inventories are required.");
        if (ReferenceEquals(source, destination))
            return GiftTransferResult.Failed("Source and destination inventories must be different.");
        if (item == null || quantity <= 0)
            return GiftTransferResult.Failed("A valid item and positive quantity are required.");
        if (!source.HasItem(item, quantity))
            return GiftTransferResult.Failed("The NPC no longer owns the requested item.");
        if (!destination.CanAddItem(item, quantity))
            return GiftTransferResult.Failed("The player's inventory is full.");

        if (!source.TryTransferItemTo(
                destination,
                item,
                quantity,
                out InventoryModel.InventorySnapshot _,
                out InventoryModel.InventorySnapshot _))
        {
            return GiftTransferResult.Failed("The inventory state changed before the gift could complete.");
        }

        source.NotifyChanged();
        destination.NotifyChanged();

        if (recipient != null && recipient.TryGetComponent<CharacterStatistics>(out var stats))
            stats.RecordItemGathered(quantity);

        QuestEventBus.Raise("ItemCollected", item.itemId, quantity);
        QuestEventBus.Raise("ItemGifted", item.itemId, quantity);
        return GiftTransferResult.Succeeded(quantity);
    }
}

/// <summary>
/// Immutable result returned by InventoryTransferService.TryGive.
/// Unity setup: none; created by InventoryTransferService.
/// </summary>
public sealed class GiftTransferResult
{
    public bool Success { get; private set; }
    public int QuantityTransferred { get; private set; }
    public string Message { get; private set; }

    internal static GiftTransferResult Succeeded(int quantity) => new GiftTransferResult
    {
        Success = true,
        QuantityTransferred = quantity,
        Message = string.Empty,
    };

    internal static GiftTransferResult Failed(string message) => new GiftTransferResult
    {
        Success = false,
        QuantityTransferred = 0,
        Message = message ?? string.Empty,
    };
}
