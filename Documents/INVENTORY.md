# Inventory System

## Overview

The inventory is a grid-based system split cleanly into data and UI layers. The data model is pure C# with no Unity dependency — the UI layer subscribes to its `OnChanged` event and redraws.

```
InventoryUI (MonoBehaviour, singleton)
  └── InventoryModel (plain C#)       — grid of InventorySlots
        └── InventorySlot[]            — item + quantity per cell

Canvas
  ├── InventoryUI                      — panel, grid, drag ghost
  │     └── InventorySlotUI × N        — per-slot view + interaction
  ├── InventoryTooltip                 — static Show/Hide
  └── InventoryContextMenu             — static Show/Hide
```

---

## InventoryModel

**File:** `Assets/Scripts/Inventory/InventoryModel.cs`

Pure C# class. Owns the slot array and all mutation logic. Fires `OnChanged` after every mutation — the UI subscribes and redraws.

### Constructor

```csharp
var model = new InventoryModel(rows: 5, columns: 6);
```

`InventoryUI` creates this automatically. Access via `InventoryUI.Model`.

### Key methods

| Method | Returns | Description |
|---|---|---|
| `AddItem(ItemData, int)` | `int` leftover | Fills partial stacks first, then empty slots. Returns unplaced amount. |
| `RemoveItem(ItemData, int)` | `bool` success | Fails if not enough held. |
| `HasItem(ItemData, int)` | `bool` | Checks total count across all slots. |
| `CountItem(ItemData)` | `int` | Total quantity across all slots. |
| `MoveSlot(from, to)` | — | Merges same-item stacks; otherwise swaps. |
| `SplitStack(slotIndex)` | `bool` success | Splits half into first empty slot. |
| `Sort()` | — | Sorts by `ItemType` then alphabetically. |
| `GetSlot(index)` | `InventorySlot` | Direct slot access. |

### Global access

```csharp
// From anywhere
InventoryUI.Model.AddItem(healthPotionData, 3);
bool hasKey = InventoryUI.Model.HasItem(goldenKeyData);
```

---

## ItemData

**File:** `Assets/Scripts/Inventory/ItemData.cs`

`ScriptableObject`. Create via **Assets → Create → Inventory → Item Data**.

### Fields

| Field | Description |
|---|---|
| Item Name | Display name |
| Icon | `Sprite` shown in the slot |
| Description | Tooltip body text |
| Type | `ItemType` enum — used for sorting and filtering |
| Flags | `ItemFlags` bitmask: `None`, `Unique`, `Quest`, `Consumable` |
| Is Stackable | Enables stack merging |
| Max Stack Size | Maximum per slot (ignored if not stackable) |

### Quest system integration

`HasItem` condition in quest JSON maps to `ItemData` by `itemId` (the asset name). The quest system calls `InventoryUI.Model.HasItem` internally.

---

## InventoryUI

**File:** `Assets/Scripts/Inventory/InventoryUI.cs`

Singleton MonoBehaviour (`DontDestroyOnLoad`). Manages the panel visibility, builds the slot grid on `Awake`, and wires up drag-and-drop.

### Inspector fields (required)

| Field | Description |
|---|---|
| Rows / Columns | Grid dimensions (default 5×6 = 30 slots) |
| Slot Prefab | Prefab with `InventorySlotUI` component |
| Panel Root | Root `RectTransform` that is shown/hidden |
| Grid Container | Parent `RectTransform` for slot prefabs (needs `GridLayoutGroup`) |
| Drag Ghost Image | `Image` that follows the cursor during drag |
| Sort Button | Calls `model.Sort()` on click |
| Close Button | Calls `Close()` on click |
| Player Controller | Auto-found if null — used to lock movement while open |

### Opening / closing

| Action | Method |
|---|---|
| Toggle | `I` key / `InventoryUI.Instance.Toggle()` |
| Open | `InventoryUI.Instance.Open()` |
| Close | `InventoryUI.Instance.Close()` |

Opening locks player movement via `PlayerController2D.SetMovementEnabled(false)`. Closing restores it.

---

## InventorySlotUI

**File:** `Assets/Scripts/Inventory/InventorySlotUI.cs`

One per grid cell. Implements Unity's drag-and-drop event interfaces. Raises events that `InventoryUI` handles:

| Event | When |
|---|---|
| `DragStarted(slotIndex)` | Drag begins on a non-empty slot |
| `DragEnded(slotIndex)` | Drag finishes (always fires) |
| `Dropped(slotIndex)` | This slot is the drop target |
| `RightClicked(slotIndex, screenPos)` | Right-click on a non-empty slot |

Hover triggers `InventoryTooltip.Show` / `.Hide` automatically.

---

## InventoryTooltip

**File:** `Assets/Scripts/Inventory/InventoryTooltip.cs`

Static show/hide calls. Place one instance under the Canvas.

```csharp
InventoryTooltip.Show(itemData);
InventoryTooltip.Hide();
```

---

## InventoryContextMenu

**File:** `Assets/Scripts/Inventory/InventoryContextMenu.cs`

Static show/hide calls. Appears on right-click. Place one instance under the Canvas.

```csharp
InventoryContextMenu.Show(model, slotIndex, screenPosition);
InventoryContextMenu.Hide();
```

---

## Canvas Setup

```
Canvas (Screen Space – Overlay)
  └── InventoryUI
        ├── PanelRoot
        │     └── GridContainer         ← GridLayoutGroup auto-added by InventoryUI
        │           └── SlotPrefab × N  ← instantiated at runtime
        ├── DragGhost (Image, raycastTarget = false)
        ├── SortButton
        ├── CloseButton
        InventoryTooltip
        InventoryContextMenu
```

---

## Giving Items from Other Systems

```csharp
// Quest action: GiveItem
InventoryUI.Model.AddItem(itemData, count);

// Quest action: RemoveItem
InventoryUI.Model.RemoveItem(itemData, count);

// Quest condition: HasItem
bool ok = InventoryUI.Model.HasItem(itemData, count);
```

Raise a quest event after a pickup:

```csharp
QuestEventBus.Raise("ItemCollected", itemData.name);
```
