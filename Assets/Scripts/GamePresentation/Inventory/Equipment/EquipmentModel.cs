using System;
using System.Collections.Generic;

/// <summary>
/// Pure-data equipment container — no MonoBehaviour, no Unity dependencies.
/// Holds at most one ItemData per EquipSlotType and enforces slot-type matching
/// (item.equipSlot must equal the target slot and item.IsEquip must be true).
///
/// Unity setup: none.
///   Created and owned by EquipmentManager.
///   Subscribe to OnSlotChanged to react to equip / unequip events.
///
/// Runtime API:
///   ItemData old = model.Equip(EquipSlotType.Weapon, swordData);   // returns displaced item
///   ItemData old = model.Unequip(EquipSlotType.Armor);             // returns removed item
///   ItemData cur = model.GetEquipped(EquipSlotType.Accessory);     // null if empty
///   bool empty   = model.IsSlotEmpty(EquipSlotType.Weapon);
/// </summary>
public class EquipmentModel
{
    private readonly Dictionary<EquipSlotType, ItemData> _slots = new();

    /// <summary>
    /// Fired when any slot changes.
    /// Args: (slot, newItem, replacedItem). newItem is null on unequip;
    /// replacedItem is null if the slot was previously empty.
    /// </summary>
    public event Action<EquipSlotType, ItemData, ItemData> OnSlotChanged;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Equip <paramref name="item"/> into <paramref name="slot"/>.
    /// The item must have IsEquip == true and item.equipSlot must match the target slot.
    /// Returns the item that was previously in the slot (may be null).
    /// Returns null and does nothing if the item fails validation.
    /// </summary>
    public ItemData Equip(EquipSlotType slot, ItemData item)
    {
        if (item == null || !item.IsEquip || item.equipSlot != slot)
            return null;

        _slots.TryGetValue(slot, out ItemData old);
        _slots[slot] = item;
        OnSlotChanged?.Invoke(slot, item, old);
        return old;
    }

    /// <summary>
    /// Remove the item from <paramref name="slot"/> and return it.
    /// Returns null if the slot was already empty.
    /// </summary>
    public ItemData Unequip(EquipSlotType slot)
    {
        if (!_slots.TryGetValue(slot, out ItemData old) || old == null)
            return null;

        _slots[slot] = null;
        OnSlotChanged?.Invoke(slot, null, old);
        return old;
    }

    /// <summary>Returns the equipped item for <paramref name="slot"/>, or null if empty.</summary>
    public ItemData GetEquipped(EquipSlotType slot)
    {
        _slots.TryGetValue(slot, out ItemData item);
        return item;
    }

    /// <summary>True if nothing is equipped in <paramref name="slot"/>.</summary>
    public bool IsSlotEmpty(EquipSlotType slot) => GetEquipped(slot) == null;
}
