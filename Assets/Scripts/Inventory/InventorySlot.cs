using System;

[Serializable]
public class InventorySlot
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
