# Player System

## Overview

The player is built from three independent components that live on the same GameObject. Each has a single responsibility and communicates through direct references, not events.

```
Player (GameObject)
  ├── Rigidbody2D             (Body Type: Dynamic, Gravity Scale: 0, Freeze Rotation Z)
  ├── BoxCollider2D / CapsuleCollider2D
  ├── PlayerController2D      — movement
  ├── PlayerInteractionController — NPC interaction & dialogue
  └── EntityStats             — HP and MP (auto-required by PlayerController2D)
```

---

## PlayerController2D

**File:** `Assets/Scripts/PlayerController2D.cs`

Handles top-down movement via `Rigidbody2D.linearVelocity`. Supports both the new Input System and legacy `Input` API via compile-time guards.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Move Speed | 6 | Units per second |
| Lock Rotation | true | Freezes Z rotation on `Rigidbody2D` |
| Force No Gravity | true | Sets `gravityScale = 0` on `Rigidbody2D` |

### API

```csharp
playerController.SetMovementEnabled(false); // freeze movement (used by dialogue/inventory)
bool moving = playerController.MovementEnabled;
```

### Input

| Action | New Input System | Legacy |
|---|---|---|
| Move | WASD / Arrow Keys / Left Stick | `Horizontal` + `Vertical` axes |

Movement is clamped to magnitude 1 so diagonal speed is not faster than straight movement.

### RequireComponent

`[RequireComponent(typeof(Rigidbody2D))]` and `[RequireComponent(typeof(EntityStats))]` — both components are always present alongside `PlayerController2D`.

---

## PlayerInteractionController

**File:** `Assets/Scripts/PlayerInteractionController.cs`

Runs an `OverlapCircleNonAlloc` search each frame the player presses E. Finds the nearest `NpcDialogue` within range, then drives the dialogue loop until the conversation ends or the player walks away.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Interaction Search Radius | 2 | World-space radius for NPC scan |
| Npc Layers | DefaultRaycastLayers | Layer mask for NPC colliders |
| Legacy Interact Key | E | Fallback when Input System is off |

### Dialogue loop

1. Player presses **E** → `OverlapCircle` finds nearest interactable NPC.
2. Player movement is locked via `playerController.SetMovementEnabled(false)`.
3. Controller walks through `DialogueNodeDefinition` nodes by calling `NpcDialogue.TryGetNode`.
4. Multi-choice nodes: **W/Up Arrow** / **S/Down Arrow** to move selection.
5. **E** advances; **Escape** or walking out of range ends the conversation.
6. On end: movement re-enabled, `DialogueUIController.HideDialogue()` called.

### Input

| Action | New Input System | Legacy |
|---|---|---|
| Interact / Advance | E / Gamepad South | `legacyInteractKey` |
| Cancel | Escape / Gamepad East | `KeyCode.Escape` |
| Choice Up | W / Up Arrow / DPad Up | `KeyCode.W` or `KeyCode.UpArrow` |
| Choice Down | S / Down Arrow / DPad Down | `KeyCode.S` or `KeyCode.DownArrow` |

---

## EntityStats (Player)

**File:** `Assets/Scripts/EntityStats.cs`

See [STATS.md](STATS.md) for full documentation. On the player, configure `Max Hp`, `Starting Hp`, `Max Mp`, `Starting Mp` from the Inspector. `Awake()` initialises from those values automatically.

---

## PlayerStatsUI

**File:** `Assets/Scripts/PlayerStatsUI.cs`

Attach to a UI GameObject in your Canvas. Subscribes to `EntityStats.OnHpChanged` / `OnMpChanged` and updates two `Image` fills.

### Setup

1. Create a Canvas (Screen Space – Overlay).
2. For each bar: background `Image` + child "Fill" `Image` set to `Image Type = Filled`, `Fill Method = Horizontal`.
3. Assign the Fill Images to `hpFill` / `mpFill` in the Inspector.
4. Leave `playerStats` empty — it calls `FindFirstObjectByType<EntityStats>()` on `Start`.

---

## Adding the Player to a Scene

1. Create a GameObject named `Player`.
2. Add `Rigidbody2D` (Body Type: Dynamic, Gravity Scale: 0, Freeze Rotation Z: ✓).
3. Add `BoxCollider2D` or `CapsuleCollider2D`.
4. Add `PlayerController2D` — Unity will auto-add `EntityStats`.
5. Add `PlayerInteractionController`.
6. Tag the GameObject `Player` (required for portal detection).
