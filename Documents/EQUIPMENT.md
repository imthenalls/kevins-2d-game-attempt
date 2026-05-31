# Equipment System

## Overview

The equipment system lets any entity (player or NPC) wear up to three items — one per slot — and automatically applies their stat bonuses to `EntityStats`.

```
EquipmentManager (MonoBehaviour)
  └── EquipmentModel (pure C#)
        └── Dictionary<EquipSlotType, ItemData>   — Weapon | Armor | Accessory
```

Stat bonuses defined on `ItemData` (`bonusMaxHp`, `bonusMaxMp`, `bonusAttack`, `bonusDefense`) are applied to `EntityStats` on equip and removed on unequip.

---

## Files

| File | Description |
|---|---|
| `Assets/Scripts/Inventory/Equipment/EquipSlotType.cs` | Enum: `Weapon`, `Armor`, `Accessory` |
| `Assets/Scripts/Inventory/Equipment/EquipmentModel.cs` | Pure C# data container; enforces slot-type matching |
| `Assets/Scripts/Inventory/Equipment/EquipmentManager.cs` | MonoBehaviour; owns the model, applies bonuses to EntityStats |

---

## Setting Up an Equipment Item

1. Create an `ItemData` asset (right-click → Inventory → Item).
2. Set **Type** to `Equipment`.
3. Set **Equip Slot** to `Weapon`, `Armor`, or `Accessory`.
4. Fill in any bonus fields: **Bonus Max Hp**, **Bonus Max Mp**, **Bonus Attack**, **Bonus Defense**.
5. Items without bonuses work fine — leave bonus fields at 0.

**In `items.json`** (for runtime-loaded items):

```json
{
  "id": "iron_sword",
  "name": "Iron Sword",
  "type": "Equipment",
  "equipSlot": "Weapon",
  "bonusAttack": 10,
  "bonusMaxHp": 0,
  "bonusMaxMp": 0,
  "bonusDefense": 0,
  "maxStackSize": 1,
  "sellValue": 50
}
```

---

## Adding EquipmentManager to an Entity

### Player

1. Select the **Player** GameObject.
2. Click **Add Component → Equipment Manager**.
3. `EntityStats` is added automatically (via `RequireComponent`) if not already present.

### NPC / Enemy

1. Select the NPC prefab.
2. Click **Add Component → Equipment Manager**.
3. Use `NpcController` or a custom script to call `Equip()` on start to pre-equip items.

---

## Runtime API

```csharp
// Get the component
var equipment = GetComponent<EquipmentManager>();

// Equip an item — returns the displaced item (put it back in inventory if not null)
ItemData displaced = equipment.Equip(EquipSlotType.Weapon, swordData);

// Unequip — returns the removed item
ItemData removed = equipment.Unequip(EquipSlotType.Armor);

// Read current loadout
ItemData current = equipment.Model.GetEquipped(EquipSlotType.Accessory);
bool empty       = equipment.Model.IsSlotEmpty(EquipSlotType.Weapon);

// Subscribe to changes (for UI refresh, autosave, etc.)
equipment.Model.OnSlotChanged += (slot, newItem, oldItem) => { /* refresh UI */ };
```

---

## EntityStats Bonus Properties

`EntityStats` now exposes:

| Property | Description |
|---|---|
| `BonusAttack` | Accumulated attack bonus from all equipped items |
| `BonusDefense` | Accumulated defense bonus from all equipped items |

HP and MP maximums are raised directly on the stat component when items are equipped, and lowered (clamping current values) when unequipped.

---

## Validation Rules

`EquipmentModel.Equip()` silently rejects an item and returns `null` if:
- `item` is null
- `item.IsEquip` is false (type is not `Equipment`)
- `item.equipSlot` does not match the target slot

Check the return value — if it's `null` and you passed a non-null item, the item was rejected.
