using Game.Core;
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
///   5. For scene-authored starting gear, enter item IDs from items.json in Starting Loadout.
///
/// Runtime API:
///   ItemData displaced = equipManager.Equip(EquipSlotType.Weapon, swordData);
///   bool equipped = equipManager.TryEquipFromInventory(inventory, swordData, out displaced);
///   ItemData removed   = equipManager.Unequip(EquipSlotType.Armor);
///   ItemData current   = equipManager.Model.GetEquipped(EquipSlotType.Accessory);
/// </summary>
[RequireComponent(typeof(EntityStats))]
[DisallowMultipleComponent]
public class EquipmentManager : MonoBehaviour
{
    [Header("Config (Game.Data)")]
    [SerializeField] private EquipmentLoadoutConfig config = new EquipmentLoadoutConfig();

    private EntityStats _stats;

    /// <summary>The underlying data model. Subscribe to Model.OnSlotChanged for change events.</summary>
    public EquipmentModel Model { get; private set; }

    private void Awake()
    {
        _stats = GetComponent<EntityStats>();
        Model  = new EquipmentModel();
        Model.OnSlotChanged += HandleSlotChanged;
    }

    private void Start()
    {
        EquipStartingItem(EquipSlotType.Weapon, config.StartingWeaponItemId);
        EquipStartingItem(EquipSlotType.Armor, config.StartingArmorItemId);
        EquipStartingItem(EquipSlotType.Accessory, config.StartingAccessoryItemId);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Equip <paramref name="item"/> into <paramref name="slot"/>.
    /// The item must be type Equipment with a matching equipSlot.
    /// Returns the displaced item (if any) so the caller can return it to inventory.
    /// Returns null if the item is invalid for the slot.
    /// </summary>
    public ItemData Equip(EquipSlotType slot, ItemData item) => Model.Equip(slot, item) as ItemData;

    /// <summary>
    /// Removes one equipment item from an inventory and equips it. Any displaced item is
    /// returned to that inventory. Unexpected placement failure rolls the operation back.
    /// </summary>
    public bool TryEquipFromInventory(
        InventoryModel inventory,
        ItemData item,
        out ItemData displaced)
    {
        displaced = null;
        if (inventory == null || item == null || !item.IsEquip || !inventory.HasItem(item))
            return false;

        if (!inventory.RemoveItem(item, 1))
            return false;

        displaced = Model.Equip(item.equipSlot, item) as ItemData;
        if (displaced == null)
            return true;

        if (inventory.AddItem(displaced, 1) == 0)
            return true;

        ItemData newlyEquipped = Model.Equip(item.equipSlot, displaced) as ItemData;
        if (newlyEquipped != null)
            inventory.AddItem(newlyEquipped, 1);
        displaced = null;
        return false;
    }

    /// <summary>
    /// Remove the item from <paramref name="slot"/> and return it.
    /// Returns null if the slot was already empty.
    /// </summary>
    public ItemData Unequip(EquipSlotType slot) => Model.Unequip(slot) as ItemData;

    private void EquipStartingItem(EquipSlotType slot, string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;

        ItemData item = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(itemId) : null;
        if (item == null)
        {
            Debug.LogWarning($"[EquipmentManager] Starting item '{itemId}' was not found on {name}.");
            return;
        }

        if (!item.IsEquip || item.equipSlot != slot)
        {
            Debug.LogWarning($"[EquipmentManager] Starting item '{itemId}' does not fit {slot} on {name}.");
            return;
        }

        Model.Equip(slot, item);
    }

    // ── Stat application ──────────────────────────────────────────────────────

    private void HandleSlotChanged(EquipSlotType slot, IItem newItem, IItem oldItem)
    {
        ItemData oldData = oldItem as ItemData;
        if (oldData != null)
            _stats.RemoveStatBonus(oldData.bonusMaxHp, oldData.bonusMaxMp,
                                   oldData.bonusAttack, oldData.bonusDefense);

        ItemData newData = newItem as ItemData;
        if (newData != null)
            _stats.ApplyStatBonus(newData.bonusMaxHp, newData.bonusMaxMp,
                                  newData.bonusAttack, newData.bonusDefense);
    }
}
