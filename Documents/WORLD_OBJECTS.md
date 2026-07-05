# World Objects System

## Overview

The World Objects system covers all player-interactable objects placed in the scene that are **not** NPCs: chests, signs, shrines, notice boards, loot drops, etc. It consists of four parts:

| Script | Location | Purpose |
|---|---|---|
| `IInteractable.cs` | `Assets/Scripts/World/` | Interface — contract for all interactable world objects |
| `WorldObject.cs` | `Assets/Scripts/World/` | Built-in implementation: text lines + optional item reward |
| `ItemPickup.cs` | `Assets/Scripts/World/` | Auto-collected item on the ground (trigger-based) |
| `InventoryHelper.cs` | `Assets/Scripts/Inventory/` | Static utility for giving items from any system |

`PlayerInteractionController` was extended to discover and drive `IInteractable` objects automatically — no extra wiring needed on the player.

---

## ItemPickup

A world item that is **automatically collected** when the player walks over it.

### Unity Setup

1. Create a GameObject with a `SpriteRenderer` and any `Collider2D`.
2. Add `ItemPickup`.
3. Assign **Item** — the `ItemData` ScriptableObject.
4. Set **Quantity** (default 1).
5. Set **Player Layers** to the Physics layer(s) your Player is on.

### Behaviour

- On trigger overlap with the player layer: calls `InventoryHelper.GiveItem`.
- If the inventory is full, the item stays on the ground (quantity unchanged) and tries again next overlap.
- Partially full inventory: takes what fits, reduces quantity on the ground.
- Destroys itself when fully collected.

---

## WorldObject

A world object the player **walks up to and presses E** to interact with. Uses the existing dialogue UI to page through text, then optionally gives an item.

### Unity Setup

1. Create a GameObject with a `SpriteRenderer` and any `Collider2D`.
2. Add `WorldObject`.
3. Set the GameObject's **physics layer** to match the **Interactable Layers** mask on `PlayerInteractionController` (recommended: create a dedicated `Interactable` layer).
4. Configure Inspector fields:

| Field | Description |
|---|---|
| **Display Name** | Header shown in the dialogue box (e.g. `"Notice Board"`) |
| **Interaction Range** | How close the player must be to trigger E |
| **Lines** | Array of text pages — player presses E to advance |
| **Reward Item** | `ItemData` given when the last line is read (leave blank for none) |
| **Reward Quantity** | Number of reward items (default 1) |
| **One Time Only** | Disables the object after the first complete read; uncheck for re-readable signs |

### Behaviour

- Player presses **E** → dialogue box opens with the first line.
- Player presses **E** to advance through lines; **Escape** to cancel.
- On cancel: no reward, object resets to first line.
- On completion (last line read): reward item given via `InventoryHelper.GiveItem`, then `QuestEventBus.Raise("ObjectInteracted", displayName)` fires. If **One Time Only**, object disables itself.

### Quest Integration (automatic)

```
QuestEventBus.Raise("ObjectInteracted", displayName)
QuestEventBus.Raise("ItemCollected",    rewardItem.itemId, taken)  // if reward given
```

---

## IInteractable (Custom Interactables)

Implement `IInteractable` on any `MonoBehaviour` to make it work with `PlayerInteractionController` without modifying any existing scripts.

```csharp
public class MyInteractable : MonoBehaviour, IInteractable
{
    public bool   CanInteract(Vector3 worldPos) => /* range / state check */;
    public string GetDisplayName()              => "My Object";
    public bool   TryGetCurrentLine(out string line) { line = "Hello!"; return true; }
    public void   Advance()                     { /* move to next state */ }
    public void   EndInteraction(GameObject interactor) { /* rewards, events */ }
}
```

Place the GameObject on a layer included in **Interactable Layers** on `PlayerInteractionController`.

---

## InventoryHelper

Static utility that centralises the three-step item-give pattern used by all world systems.

```csharp
// Give item to player, notify CharacterStatistics, fire QuestEventBus
int taken = InventoryHelper.GiveItem(itemData, quantity, recipientGameObject);
// taken == 0  →  inventory was full, nothing added
// taken < qty →  partial (inventory had limited space)
```

| Parameter | Notes |
|---|---|
| `item` | The `ItemData` to add |
| `quantity` | How many to try to add |
| `recipient` | Optional `GameObject` used to find `CharacterStatistics`. Pass `null` to skip stat tracking. |

Use `InventoryHelper.GiveItem` in any new system that gives items to the player (shop purchases, quest cutscenes, loot drops, etc.).

---

## PlayerInteractionController Changes

`PlayerInteractionController` was extended with:

- **Interactable Layers** (`LayerMask`) — set to the layer(s) your `IInteractable` objects are on.
- When E is pressed and no NPC dialogue is found, the controller searches for the nearest `IInteractable` within the same `interactionSearchRadius`.
- NPC dialogues take **priority** — if both an NPC and a `WorldObject` are in range, the NPC is preferred.
- Escape cancels an active `IInteractable` interaction the same way it cancels NPC dialogue.
