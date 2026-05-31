using UnityEngine;

/// <summary>
/// Adds an equipment loadout to any entity (player or NPC).
/// Owns an EquipmentModel and automatically applies / removes ItemData stat bonuses
/// on EntityStats whenever an item is equipped or unequipped.
///
/// Unity setup:
///   1. Add EquipmentManager to the same GameObject as EntityStats
///      (EntityStats is added automatically via RequireComponent).
///   2. Call Equip(slot, itemData) to equip an item; the displaced item (if any)
///      is returned so you can move it back to the inventory.
///   3. Call Unequip(slot) to remove an item; the removed item is returned.
///   4. Subscribe to Model.OnSlotChanged for UI or external reactions.
///
/// Runtime API:
///   ItemData displaced = equipManager.Equip(EquipSlotType.Weapon, swordData);
///   ItemData removed   = equipManager.Unequip(EquipSlotType.Armor);
///   ItemData current   = equipManager.Model.GetEquipped(EquipSlotType.Accessory);
/// </summary>
[RequireComponent(typeof(EntityStats))]
[DisallowMultipleComponent]
public class EquipmentManager : MonoBehaviour
{
    private EntityStats _stats;

    /// <summary>The underlying data model. Subscribe to Model.OnSlotChanged for change events.</summary>
    public EquipmentModel Model { get; private set; }

    private void Awake()
    {
        _stats = GetComponent<EntityStats>();
        Model  = new EquipmentModel();
        Model.OnSlotChanged += HandleSlotChanged;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Equip <paramref name="item"/> into <paramref name="slot"/>.
    /// The item must be type Equipment with a matching equipSlot.
    /// Returns the displaced item (if any) so the caller can return it to inventory.
    /// Returns null if the item is invalid for the slot.
    /// </summary>
    public ItemData Equip(EquipSlotType slot, ItemData item) => Model.Equip(slot, item);

    /// <summary>
    /// Remove the item from <paramref name="slot"/> and return it.
    /// Returns null if the slot was already empty.
    /// </summary>
    public ItemData Unequip(EquipSlotType slot) => Model.Unequip(slot);

    // ── Stat application ──────────────────────────────────────────────────────

    private void HandleSlotChanged(EquipSlotType slot, ItemData newItem, ItemData oldItem)
    {
        if (oldItem != null)
            _stats.RemoveStatBonus(oldItem.bonusMaxHp, oldItem.bonusMaxMp,
                                   oldItem.bonusAttack, oldItem.bonusDefense);

        if (newItem != null)
            _stats.ApplyStatBonus(newItem.bonusMaxHp, newItem.bonusMaxMp,
                                  newItem.bonusAttack, newItem.bonusDefense);
    }
}
