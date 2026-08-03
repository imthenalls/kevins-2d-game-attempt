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
| `Assets/Scripts/Inventory/Equipment/EquippedWeaponVisual.cs` | Shows the equipped Weapon sprite on an entity visual child |

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

The Player in `NewScene` already has `EquipmentManager`. Right-click an equipment item in
the inventory and choose **Equip**. The item moves into its matching equipment slot; an item
previously in that slot returns to the inventory.

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

## Equipment Canvas

`NewScene` contains a real, editable `EquipmentPanel` under `InventoryCanvas`. It opens and
closes with the inventory and is positioned on the right side of the Canvas through its
serialized `RectTransform`.

The panel contains three `EquipmentSlotUI` drop targets:

| Slot | Accepted item setting |
|---|---|
| Weapon | `type: Equipment`, `equipSlot: Weapon` |
| Armor | `type: Equipment`, `equipSlot: Armor` |
| Accessory | `type: Equipment`, `equipSlot: Accessory` |

Drag an equipment item from the inventory onto its matching slot. Compatible slots highlight
green. A replaced item returns to the inventory. Right-click an equipped item to unequip it;
the action is rejected if the inventory has no room.

Scene hierarchy and Inspector wiring:

```
InventoryCanvas
  EquipmentPanel                 EquipmentUI + Image
    EquipmentTitle               TextMeshProUGUI
    EquipmentLabels              TextMeshProUGUI
    WeaponSlot                   EquipmentSlotUI + Image
      Icon                       Image
      ItemName                   TextMeshProUGUI
    ArmorSlot                    EquipmentSlotUI + Image
      Icon                       Image
      ItemName                   TextMeshProUGUI
    AccessorySlot                EquipmentSlotUI + Image
      Icon                       Image
      ItemName                   TextMeshProUGUI
```

On `EquipmentUI`, assign the panel `RectTransform` and all three `EquipmentSlotUI` objects.
On each slot, assign its type, background Image, icon Image, and item-name TMP text. The
`NewScene` references are already wired. `InventoryUI.GetOrCreate(panelRoot)` finds this
scene component and synchronizes its visibility. Runtime generation remains only as a
fallback for older scenes that do not yet contain an `EquipmentPanel`.

## Equipped Weapon Visual

`NewScene` includes a `WeaponVisual` child under `PlayerVisual`. Because `PlayerVisual`
rotates with movement facing, the displayed weapon follows the character direction.

```
Player
  EquipmentManager
  PlayerVisual
    WeaponVisual                 SpriteRenderer + EquippedWeaponVisual
```

`EquippedWeaponVisual` subscribes to `EquipmentModel.OnSlotChanged`. Equipping a Weapon
places that item's icon sprite on the world SpriteRenderer; unequipping clears and hides it.
The scene renderer uses sorting order 2 so the sword appears over the character. Its local
position, rotation, and scale can be adjusted directly on `WeaponVisual` in the Inspector.

This component only displays the held weapon. Swing animation and attack timing are handled
separately so visual motion can later be synchronized with `CombatAttacker`.

---

## EntityStats Bonus Properties

`EntityStats` now exposes:

| Property | Description |
|---|---|
| `BonusAttack` | Accumulated attack bonus from all equipped items |
| `BonusDefense` | Accumulated defense bonus from all equipped items |

HP and MP maximums are raised directly on the stat component when items are equipped, and lowered (clamping current values) when unequipped.

`CombatAttacker` adds `EntityStats.BonusAttack` to each successful hit. The equipped
`iron_sword` supplies a +10 attack bonus through `items.json`.

## Save Integration

Save version 3 stores equipped player items separately from the inventory grid. Loading
restores base stats and inventory first, then equips saved items so bonuses apply once.

---

## Validation Rules

`EquipmentModel.Equip()` silently rejects an item and returns `null` if:
- `item` is null
- `item.IsEquip` is false (type is not `Equipment`)
- `item.equipSlot` does not match the target slot

Check the return value — if it's `null` and you passed a non-null item, the item was rejected.
