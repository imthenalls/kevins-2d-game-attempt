# Sliding Doors

## Overview

`SlidingDoor` is a reusable single-panel wall door. Pressing **E** near it slides the panel
sideways like one half of an elevator door. The solid panel collider disables when fully
open so the player can pass, while a separate trigger remains available for interaction.
Press E again to close it when **Can Close** is enabled.

Doors can require a key from `PlayerKeyring`. The ready-made prefab requires `golden_key`:
without that key, pressing E leaves the door closed. The door remembers a successful unlock by
default, and can optionally consume one key when unlocking.

## Ready-made prefab

Use `Assets/Prefabs/SlidingDoor.prefab`:

1. Drag the prefab from the Project window into the scene.
2. Position the root in the opening in your wall.
3. Rotate the root if the wall runs in a different direction; the local **Open Offset**
   rotates with it.
4. Resize the `DoorPanel` child if the opening is wider or taller.
5. Confirm the prefab's layer is included in
   `PlayerInteractionController.Interactable Layers`.

No player changes are required. The existing interaction controller discovers the door
through `IInteractable`.

## Prefab hierarchy

```text
SlidingDoor
  SlidingDoor
  CircleCollider2D (trigger; interaction remains active while open)
  DoorPanel
    SpriteRenderer
    BoxCollider2D (solid; disables after opening)
```

## Inspector settings

| Field | Default | Purpose |
|---|---:|---|
| Sliding Panel | DoorPanel | Transform that moves |
| Blocking Collider | DoorPanel BoxCollider2D | Blocks passage while closed |
| Open Offset | (1.25, 0) | Local distance and direction the panel slides |
| Slide Duration | 0.45 s | Time for opening or closing |
| Display Name | Door | Interaction name |
| Interaction Range | 2 | Maximum E interaction distance |
| Starts Open | Off | Places the panel open on scene start |
| Can Close | On | Allows a second E press to close it |
| Required Key Id | golden_key | ItemDatabase id required to unlock; leave blank for no lock |
| Consume Key On Unlock | Off | Removes one matching key when the lock succeeds |
| Remain Unlocked | On | Remembers the unlock so later openings do not require another key |

For a door that slides left, set **Open Offset X** to `-1.25`. For vertical movement, use
the Y value instead.

## Runtime API

```csharp
door.Open();
door.Close();
door.Toggle();
bool opened = door.TryOpen(playerGameObject);

bool open = door.IsOpen;
bool moving = door.IsMoving;
bool locked = door.IsLocked;

door.OnUnlocked += HandleUnlocked;
door.OnUnlockFailed += HandleUnlockFailed;
door.OnOpened += HandleOpened;
door.OnClosed += HandleClosed;
```

## Rebuilding the prefab

The editor creates the prefab automatically if it is missing. To intentionally rebuild it,
choose **Tools > World > Create Sliding Door Prefab**. Rebuilding replaces prefab-level
settings, so make variants when you want to preserve custom appearances.
