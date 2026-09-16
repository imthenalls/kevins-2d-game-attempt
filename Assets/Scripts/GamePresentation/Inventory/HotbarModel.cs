using System;

/// <summary>
/// Pure-data model for the hotbar. Holds up to <see cref="SlotCount"/> ItemData
/// assignments (not copies — just which item type is "pinned" to each slot).
/// Using a slot removes one of that item from the player's InventoryModel.
///
/// Not a MonoBehaviour — created and owned by HotbarUI.
/// Access at runtime via HotbarUI.Model.
/// </summary>
public class HotbarModel
{
    public const int SlotCount = 6;

    private readonly ItemData[] slots = new ItemData[SlotCount];

    /// <summary>Fires after any assignment or clear.</summary>
    public event Action OnChanged;

    /// <summary>Returns the ItemData assigned to <paramref name="index"/>, or null if empty.</summary>
    public ItemData GetSlot(int index) =>
        (index >= 0 && index < SlotCount) ? slots[index] : null;

    /// <summary>Assigns <paramref name="item"/> to <paramref name="index"/>.</summary>
    public void Assign(int index, ItemData item)
    {
        if (index < 0 || index >= SlotCount) return;
        slots[index] = item;
        OnChanged?.Invoke();
    }

    /// <summary>Clears the slot at <paramref name="index"/>.</summary>
    public void Clear(int index)
    {
        if (index < 0 || index >= SlotCount) return;
        slots[index] = null;
        OnChanged?.Invoke();
    }

    /// <summary>Returns the first empty slot index, or -1 if all slots are filled.</summary>
    public int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (slots[i] == null) return i;
        return -1;
    }
}
