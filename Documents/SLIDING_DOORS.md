# Sliding Doors (Grid Gates)

> **Game.Data config:** gate tuning and lock configuration live in the pure-C#
> `Game.Core.SlidingDoorConfig` (`Game.Data` assembly) and appear under **Settings** in the
> Inspector. The `DoorAxis` and `GateUseResult` enums also live in `Game.Data`. `SlidingDoor` keeps
> only its Unity references (`Grid`, gate `Sprite`, traveler `LayerMask`) and the stable `gateId`.

## Overview

`SlidingDoor` is a reusable, E-interactable gate that is **built from whole grid cells** rather
than a rotated rectangle. Each covered cell gets a diamond sprite positioned with
`Grid.GetCellCenterWorld`, so the gate always aligns to the tilemap. The gate is split into two
halves that retract outward by whole-cell vectors to open. A solid diamond collider per cell
blocks passage while closed. It can require a key from `PlayerKeyring`, and it tints its cells
by lock state.

> Why cells? In a 2:1 isometric grid the cell is a diamond and the two grid axes are ~126.87°
> apart. A `Transform` cannot shear, so any rectangle can be parallel to at most one axis and
> will always look oblique. Per-cell diamonds are aligned by construction.

| State | Color |
|---|---|
| Locked | `Locked Color` (default red/amber) |
| Unlocked or open | `Unlocked Color` (default cyan/teal) |

The key check, interaction flow, and public API are unchanged from the original door.

## Placement Convention

- Place the gate **root on the first cell** of the opening.
- The gate extends along **Door Axis** for **Cell Length** cells.
- At `Awake`, the root snaps to `Grid.GetCellCenterWorld(WorldToCell(position))`, so a
  slightly mis-dragged gate still lands exactly on a cell.
- **Interaction range is one tile by default**, and is measured from the interactor to the
  **nearest covered cell**, so a long gate can be opened from any of its cells.
- The door must be under its Grid in the hierarchy, or have **Grid** assigned explicitly.

## Prefab hierarchy

```text
SlidingDoor
  SlidingDoor
  CircleCollider2D              (trigger; interaction stays active while open)
  GateHalfA
    GateCell_0
      SpriteRenderer            (diamond sprite, tinted by lock state)
      PolygonCollider2D         (diamond; disabled when open)
    ...
  GateHalfB
    GateCell_...
```

The `GateHalfA` / `GateHalfB` / `GateCell_*` objects are generated automatically. `SlidingDoor`
uses `[ExecuteAlways]`, so it builds and rebuilds them in the **editor as well as at runtime** —
you always see the gate in the Scene view without entering Play mode. The generated cells are
serialized with the scene/prefab, so the preview persists even before the script runs. Never
rely on `Awake` alone for a placeable object's visuals (see **Editor Preview Rules** in
`AGENT.md`).

## Inspector settings

| Field | Default | Purpose |
|---|---:|---|
| Grid | (parent Grid) | Grid to snap to; set explicitly for prefab instances not under a Grid |
| Door Axis | GridX | Axis the cells advance along; halves retract along it |
| Cell Length | 2 | Number of cells the closed gate covers |
| Gate Sprite | GateDiamond | Diamond sprite drawn on each covered cell |
| Reverse Slide Direction | Off | Swaps which half retracts which way |
| Slide Duration | 0.45 s | Time for opening or closing |
| Close After Passing | On | Auto-closes once the last traveler leaves the gate trigger |
| Close Delay | 0.25 s | Delay before the auto close runs |
| Traveler Layers | Everything | Layers counted as travelers for the auto close |
| Display Name | Gate | Interaction name |
| Interaction Range | 1 | Maximum E interaction distance, measured to the nearest covered cell |
| Starts Open | Off | Places the gate open on scene start |
| Can Close | On | Allows a second E press to close it |
| Required Key Id | golden_key | ItemDatabase id required to unlock; blank = no lock |
| Locked Message | It's locked. You need the {0}. | Interaction text shown when the player lacks the key; `{0}` = key name |
| Consume Key On Unlock | Off | Removes one matching key when the lock succeeds |
| Remain Unlocked | On | Remembers the unlock so later openings skip the key |
| Locked Color | red/amber | Cell tint while locked |
| Unlocked Color | cyan/teal | Cell tint after unlock or while open |

## Axis directions

| Door Axis | Cells advance | Use for |
|---|---|---|
| `GridX` | along the grid +X axis (down-right diamond edge) | gates across a corridor that runs on the grid Y axis |
| `GridY` | along the grid +Y axis (up-right diamond edge) | gates across a corridor that runs on the grid X axis |

Both halves retract along the chosen axis: the lower-indexed half moves `-axis` by its own cell
count, the higher-indexed half moves `+axis` by its cell count, opening a gap exactly the width
of **Cell Length**.

## Auto Close

When **Close After Passing** is enabled, the gate closes itself after the player or an NPC walks
through it. The root interaction trigger tracks every traveler that enters; when the last one
leaves, the gate waits **Close Delay** seconds and retracts back to closed (unless the gate is
already moving, closing is disabled, or another traveler has entered).

A "traveler" is any collider on a **Traveler Layer** that has a `Rigidbody2D` or an
`IEntityController` (the shared interface implemented by both `PlayerController2D` and
`NpcController`). This means the same behavior works for the player and for enemies/NPCs without
extra wiring.

Auto close only closes a gate that is already open, so it never fights the E interaction. Press E
again to close early.

## Locked Feedback

When the player interacts with a locked gate and does **not** have the required key, the gate
shows an interaction message (via the shared `DialogueUIController`) instead of silently
failing. The message comes from **Locked Message**, with `{0}` replaced by the key's display
name from `ItemDatabase` (or the raw id if the item is unknown). Example:

```
Door
It's locked. You need the Golden Key.
```

Press Space to dismiss it. If the player **does** have the key, the message is skipped and the
gate unlocks and opens directly. The key check, `OnUnlockFailed` event, and `Debug.Log` all
still fire as before.

## Runtime API

```csharp
door.Open();
door.Close();
door.Toggle();
bool opened = door.TryOpen(playerGameObject);
GateUseResult result = door.TryUse(playerGameObject); // Opened / Locked / Busy / Unavailable

bool open = door.IsOpen;
bool moving = door.IsMoving;
bool locked = door.IsLocked;

door.OnUnlocked += HandleUnlocked;
door.OnUnlockFailed += HandleUnlockFailed;
door.OnOpened += HandleOpened;
door.OnClosed += HandleClosed;
door.OnUseResolved += (interactor, result) => { /* players and NPCs alike */ };
```

Key checks resolve the interacting entity's `IKeyHolder` (falling back to `PlayerKeyring` for the
player), so the same call works for a player or an NPC. See [KEY_HOLDER.md](KEY_HOLDER.md).

## Tools

- **Tools > World > Create Sliding Door Prefabs** rebuilds the base prefab and the 1–4 cell
  variants.
- Right-click a `SlidingDoor` component and choose **Rebuild Gate** to regenerate its cells in
  the Scene view.
- The **Door Placement Brush** paints cell-aligned gate instances and sets `Grid`, `Door Axis`,
  and `Cell Length`. It supports rectangular and isometric grids.

The gate art is `Assets/Sprites/Isometric/GateDiamond.png` (one diamond, 1 x 0.5 units). Tinting
is applied through the cell `SpriteRenderer.color`, so the sprite should stay light/neutral.
