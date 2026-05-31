using System;

/// <summary>
/// Plain data container for a single inventory slot: an ItemData reference and a quantity.
/// Not a MonoBehaviour — created and owned entirely by InventoryModel.
///
/// Unity setup: none required.
///   Access slots at runtime via InventoryModel.GetSlot(index).
///   IsEmpty is true when item is null or quantity is zero.
///   Call Set(item, qty) to fill a slot and Clear() to empty it.
/// </summary>
{
    public ItemData item;
    public int quantity;

    public bool IsEmpty => item == null || quantity <= 0;

    public void Set(ItemData newItem, int qty)
    {
        item = newItem;
        quantity = qty;
    }

    public void Clear()
    {
        item = null;
        quantity = 0;
    }
}
