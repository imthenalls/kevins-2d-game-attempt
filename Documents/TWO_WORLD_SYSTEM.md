# Two-World System

## Overview

The game supports two independent world layers, `WorldA` and `WorldB`. A portal can keep the
current world or switch worlds. Switching changes the active playable character and the
inventory model exposed through `InventoryUI.Model`.

Existing portals and items remain compatible:

- A portal with **Changes World** disabled behaves exactly as before.
- An item with no `worldScope` in `items.json` defaults to `Shared`.
- `WorldTravelState` creates itself automatically and survives scene changes.

## Runtime flow

```text
World A character enters a world-changing portal and presses G
  -> remember World A scene and position
  -> load or enter the destination
  -> set active world to World B
  -> activate the World B character
  -> switch InventoryUI.Model to World B's inventory
  -> move Shared items into the active inventory
  -> place the active character at the destination portal's Exit Point
```

## Scripts

| Script | Purpose |
|---|---|
| `WorldTravelState` | Persistent active-world state, remembered positions, and character activation |
| `WorldCharacter` | Marks a playable character as belonging to World A or World B |
| `PlayerAvatarProfile` | Defines each avatar's speed, dash behavior, identity, and starting abilities |
| `PortalTrigger2D` | Stores whether a route changes worlds, its destination world, and optional unlock flag |
| `InventoryUI` | Owns the two player inventories and exposes only the active one |
| `ItemData` | Stores `Shared`, `WorldA`, or `WorldB` item scope |

## Unity setup

### Playable characters

For each playable character prefab or scene object:

1. Add `WorldCharacter` to the character's root GameObject.
2. Set **World** to `WorldA` or `WorldB`.
3. Assign that world's `PlayerAvatarProfile` to **Profile**.
4. Keep that character's controller, collider, visuals, and character-owned camera under the
   root so disabling the root disables the whole character.
5. Keep the normal `Player` tag. The destination scene may contain only its own character, or
   both characters when using same-scene world switching.

If a scene has only one player and no `WorldCharacter`, portal travel falls back to the normal
tagged player. This keeps existing scenes working.

### World-changing portals

Configure the existing `PortalTrigger2D`:

1. Set **Destination Scene** and **Destination Portal Id** normally.
2. Enable **Changes World**.
3. Set **Destination World**.
4. Optionally enter a **Required Unlock Flag**. The portal works only after
   `WorldStateManager` contains that true flag.
5. Configure the return portal the same way, targeting the other world.

The player must remain inside a world-changing portal trigger and press **G** to use it.
Same-world portals still activate immediately when entered.

The receiving portal's **Exit Point** remains the spawn point. Cross-scene destinations must
be included in Unity Build Settings as with existing portals.

### Item scopes

JSON items may include:

```json
"worldScope": "WorldA"
```

Allowed values are:

| Value | Behavior |
|---|---|
| `WorldA` | Can exist only in the World A player inventory |
| `WorldB` | Can exist only in the World B player inventory |
| `Shared` | Moves with the player between both inventory models |

Omitting `worldScope` uses `Shared`. The inventory panel, pickups, quest item checks, trading,
and hotbar continue using `InventoryUI.Model`, which now means the active world's inventory.
Hotbar assignments for items unavailable in the destination world are cleared during travel.

Shared items need free space in the destination inventory. If it is full, the shared item stays
in the source inventory and Unity logs a warning rather than deleting it.

## Save data

Save version 5 stores:

- The active world.
- The last known scene and position for each world.
- World A and World B inventory slots separately.
- Existing shared state such as quests, world facts, keys, wallet, and player stats.

Save version 6 also stores each world's unlocked avatar ability IDs. HP, maximum HP, and Wallet
mana are captured from the outgoing avatar and restored onto the incoming avatar during travel.
See [PLAYER_AVATARS.md](PLAYER_AVATARS.md).

Older saves load into World A and migrate their existing inventory into World A. Because old
items default to `Shared`, they will follow the player the next time a world-changing portal is
used.

## Runtime API

```csharp
WorldLayer active = WorldTravelState.Instance.CurrentWorld;
WorldTravelState.Instance.SetCurrentWorld(WorldLayer.WorldB);

InventoryModel worldA = InventoryUI.Instance.GetInventoryForWorld(WorldLayer.WorldA);
InventoryModel activeInventory = InventoryUI.Model;
```

> **Hazard — the active world is process-global.** `WorldTravelState` is `DontDestroyOnLoad`, so
> `CurrentWorld` persists for the whole play session, and `WorldCharacter.SetActiveForWorld`
> deactivates the character whose world does not match. Any path that enters a scene outside the
> portal flow (fast-travel/menu return, a new-game flow, a manual `SceneLoader` load, a direct
> `LoadScene`) must call `SetCurrentWorld(...)` or rely on the scene's `WorldSceneIdentity`.
> Scenes containing a `WorldCharacter` must have exactly one `WorldSceneIdentity` — `Overworld` has
> one set to World A (root "World A"), `WorldB` has one set to World B. The data validator enforces
> this; Play Mode tests inherit `PlayModeTestBase` which resets the world to World A.

## Included World B scene

`Assets/Scenes/WorldB.unity` contains:

- A bounded 21 x 15 square tile starter room.
- A cyan placeholder World B player with its own camera.
- `WorldSceneIdentity` set to World B, so direct Play mode testing activates the right layer.
- The **isometric training wing** (entrance room, hallway with a locked gate, and arena). See
  [TRAINING_ARENA.md](TRAINING_ARENA.md).
- A `world_b_entry` arrival/return portal that returns to `world_b_portal` in `Overworld`.
- Standalone `PortalManager` and `SceneLoader` objects, which discard themselves when the
  persistent copies from another scene already exist.

To enter it from World A, `Overworld` has `portal_to_worldb` (`world_b_portal`) pointing at scene
`WorldB`, portal ID `world_b_entry`, with **Changes World** enabled and **WorldB** selected. The
player stands inside and presses G.
