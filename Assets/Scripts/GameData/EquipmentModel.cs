using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Pure-data equipment container — no MonoBehaviour, no Unity dependencies, so it lives in
    /// Game.Data and can be tested without a scene. Holds at most one <see cref="IItem"/> per
    /// EquipSlotType and enforces slot-type matching (item.EquipSlot must equal the target slot and
    /// item.IsEquip must be true).
    ///
    /// Items are held as the engine-free <see cref="IItem"/> contract; the Unity ItemData
    /// ScriptableObject implements it at runtime.
    ///
    /// Unity setup: none. Created and owned by EquipmentManager.
    ///
    /// Runtime API:
    ///   IItem old = model.Equip(EquipSlotType.Weapon, sword);   // returns displaced item
    ///   IItem old = model.Unequip(EquipSlotType.Armor);         // returns removed item
    ///   IItem cur = model.GetEquipped(EquipSlotType.Accessory); // null if empty
    ///   bool empty = model.IsSlotEmpty(EquipSlotType.Weapon);
    /// </summary>
    public class EquipmentModel
    {
        private readonly Dictionary<EquipSlotType, IItem> slots = new Dictionary<EquipSlotType, IItem>();

        /// <summary>
        /// Fired when any slot changes.
        /// Args: (slot, newItem, replacedItem). newItem is null on unequip;
        /// replacedItem is null if the slot was previously empty.
        /// </summary>
        public event Action<EquipSlotType, IItem, IItem> OnSlotChanged;

        /// <summary>
        /// Equip <paramref name="item"/> into <paramref name="slot"/>. The item must have
        /// IsEquip == true and EquipSlot matching the target slot. Returns the item previously in
        /// the slot (may be null), or null and does nothing if validation fails.
        /// </summary>
        public IItem Equip(EquipSlotType slot, IItem item)
        {
            if (item == null || !item.IsEquip || item.EquipSlot != slot)
                return null;

            slots.TryGetValue(slot, out IItem previous);
            slots[slot] = item;
            OnSlotChanged?.Invoke(slot, item, previous);
            return previous;
        }

        /// <summary>
        /// Remove the item from <paramref name="slot"/> and return it. Returns null if the slot was
        /// already empty.
        /// </summary>
        public IItem Unequip(EquipSlotType slot)
        {
            if (!slots.TryGetValue(slot, out IItem previous) || previous == null)
                return null;

            slots[slot] = null;
            OnSlotChanged?.Invoke(slot, null, previous);
            return previous;
        }

        /// <summary>Returns the equipped item for <paramref name="slot"/>, or null if empty.</summary>
        public IItem GetEquipped(EquipSlotType slot)
        {
            slots.TryGetValue(slot, out IItem item);
            return item;
        }

        /// <summary>True if nothing is equipped in <paramref name="slot"/>.</summary>
        public bool IsSlotEmpty(EquipSlotType slot) => GetEquipped(slot) == null;
    }
}
