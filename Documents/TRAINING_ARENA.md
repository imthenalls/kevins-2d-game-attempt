# Training Arena Wing

> **Game.Data config:** the spawner's interaction range and arena bounds live in the pure-C#
> `Game.Core.TrainingSpawnerConfig` (`Game.Data` assembly) and appear under **Settings** in the
> Inspector.

## Overview

The training wing now lives in the **WorldB** scene and is reached from `Overworld` through a
two-world portal. Entering it switches the active character to the **World B avatar** (its own
movement profile and inventory) via `WorldTravelState`.

It is built from the project's isometric tiles (the same diamond floor/wall tiles as
`Overworld`) plus default Unity placeholder sprites. It adds an entrance room, a hallway with a
locked gate, and a larger combat arena.

The route is:

```text
Overworld
  -> world_b_portal  (stand inside, press G)
  -> World B world_b_entry  (arrive in the entrance room, now the World B avatar)
Entrance room
  -> hallway
  -> Training Arena Locked Gate  (requires training_arena_key)
  -> arena
  -> Green Training Box -> repeatable Training Challenger
Return: stand inside world_b_entry and press G -> Overworld world_b_portal
```

## Scene location

The wing is in `Assets/Scenes/WorldB.unity`, grouped under `Training Wing`. The scene also keeps
its generated square starter room, the `World B Player`, camera, and its portal managers.

```text
World B
  World B Grid (rectangle starter room, unchanged)
  World B Player (+ camera, WorldCharacter = WorldB)
  World B Entry and Return Portal   portal id: world_b_entry
  Training Wing
    Training Grid (Isometric, cellSize 1 x 0.5)
      Floor
        Tilemap / TilemapRenderer
      Walls
        Tilemap / TilemapRenderer / TilemapCollider2D
    Arena Key Keeper
      SpriteRenderer / BoxCollider2D / NpcController / NpcDialogue
    Training Arena Locked Door
      SlidingDoor (GridY, 3 cells, training_arena_key)
    Green Training Box
      SpriteRenderer / BoxCollider2D / TrainingEnemySpawner
    Challenger Spawn Point
  World B Portal Manager / World B Scene Loader
```

## Isometric layout

The wing uses an `Isometric` Grid with cell size `(1, 0.5)`. The floor and walls are diamond
tiles; walls are one cell thick around every floor cell.

| Area | Cell rectangle (x, y, w, h) |
|---|---|
| Entrance room | `44, -4, 8, 8` |
| Hallway | `52, -1, 9, 3` |
| Arena | `61, -7, 18, 14` |

| Object | Cell |
|---|---|
| Arrival portal `world_b_entry` | `47, 0` |
| Arena Key Keeper | `45, 2` |
| Locked gate (anchor, GridY, length 3) | `56, -1` |
| Green Training Box | `64, 4` |
| Challenger spawn point | `70, 0` |

The arena `Rect` bounds passed to the spawner and dash AI are the world-space bounding box of
the arena cells.

## Portal connection

Both portals are cross-scene and **Changes World** is enabled, so travel requires standing inside
the trigger and pressing **G**.

| Scene | GameObject | Portal Id | Destination Scene | Destination Portal | Changes World | Destination World |
|---|---|---|---|---|---|---|
| Overworld | portal_to_worldb | `world_b_portal` | `WorldB` | `world_b_entry` | Yes | WorldB |
| WorldB | World B Entry and Return Portal | `world_b_entry` | `Overworld` | `world_b_portal` | Yes | WorldA |

`WorldTravelState` remembers the departing world's position, activates the destination world's
`WorldCharacter`, and swaps the inventory exposed by `InventoryUI`. See
[TWO_WORLD_SYSTEM.md](TWO_WORLD_SYSTEM.md) and [PLAYER_AVATARS.md](PLAYER_AVATARS.md).

The generated route list is recorded in `Documents/Generated/portal-map.md` and
`Documents/Generated/portal-map.json`.

## Rebuilding the wing

Open `Assets/Scenes/WorldB.unity`, then use **Tools > Worlds > Build Training Wing In WorldB**.
The builder refuses to duplicate an existing `Training Wing`. It re-creates the grid, floor,
walls, gate, keeper, spawner, and arrival-portal wiring.

## Arena Key Keeper

`Arena Key Keeper` sits in the entrance room. Its setup is:

| Field | Value |
|---|---|
| `NpcController.Npc Id` | `training_key_keeper` |
| `NpcController.Display Name` | `Arena Keeper` |
| `NpcDialogue.Dialogue Id` | `training_arena_key_gift` |
| `NpcDialogue.Gift Item Id` | `training_arena_key` |
| Gift quantity | `1` |

`Assets/StreamingAssets/items.json` defines `training_arena_key` as a unique `KeyItem`.
`Assets/StreamingAssets/npc_inventories.json` gives one copy to the keeper. Completing the
conversation transfers that owned key into the player's `PlayerKeyring`. Because the item is
unique and the NPC no longer owns it after transfer, repeating the dialogue cannot duplicate the
key.

## Locked gate

`Training Arena Locked Door` is an instance of `Assets/Prefabs/SlidingDoor.prefab`. It uses the
grid-gate layout: `Grid = Training Grid`, `Door Axis = GridY`, `Cell Length = 3`, and spans the
hallway. Its `requiredKeyId` is `training_arena_key`; the existing `golden_key` does not satisfy
this lock. The gate remains unlocked after the correct key opens it and does not consume the key.

## Green Training Box

`TrainingEnemySpawner` implements `IInteractable`, so the existing player interaction flow drives
it. Put it on a layer included by `PlayerInteractionController.Interactable Layers` if scene
layers are changed later.

Interaction behavior:

1. Press the normal interact key near the green box.
2. Confirm its dialogue line to request a challenger.
3. Cancelling the line does not spawn anything.
4. Only one living challenger can exist at a time.
5. Killing the challenger clears the occupied slot.
6. Interact again to start another round with a fresh enemy and full HP.

`TrainingEnemySpawner` Inspector fields:

| Field | Purpose |
|---|---|
| Enemy Prefab | `Assets/Prefabs/TrainingChallenger.prefab` |
| Spawn Point | Clear point inside the arena |
| Arena Bounds | World-space rectangle that contains combat |
| Interaction Range | Maximum range for the normal interaction system |
| Ready Line | Text shown when another challenger can spawn |
| Busy Line | Text shown while the current challenger is alive |

If the main spawn position is occupied, the component tries four nearby positions. It never
places an enemy outside the arena bounds or on an existing solid collider. Defeated runtime
enemies are removed so repeated rounds do not accumulate hidden bodies.

## Training Challenger prefab

`Assets/Prefabs/TrainingChallenger.prefab` uses default Unity placeholder geometry and the
project's existing iron-sword item. Its component layout is:

```text
Training Challenger
  NpcController                 NpcType Enemy, 60 HP, arena-wide aggro
  EntityStats
  CombatReceiver
  Rigidbody2D                  gravity 0, rotation frozen, continuous collision
  BoxCollider2D
  EquipmentManager             starting weapon: iron_sword
  CombatAttacker               player input off, 5 base damage
  NpcDashMeleeController
  Aim Pivot
    SpriteRenderer             default Unity square placeholder
    WeaponVisual
      SpriteRenderer           populated from iron_sword
      EquippedWeaponVisual
```

The prefab uses the stable NPC ID `training_challenger`. No loot is configured for this ID,
so repeatable rounds do not create loot piles.

## Dash attack sequence

`NpcDashMeleeController` replaces ordinary wander/proximity melee behavior for this enemy:

```text
Approach
  -> Warning: stop and turn yellow for 0.5 seconds
  -> Commit the current direction
  -> Dash in a straight line
  -> Swing the equipped sword
  -> Recovery
  -> Approach
```

The player can dodge after seeing the warning because the dash direction is committed at the
end of the warning and does not steer during the dash. Leaving the arena or killing/disabling
the enemy clears velocity, warning color, and attack state.

## Manual verification

1. In `Overworld`, stand in `world_b_portal` and press **G**; confirm the world switches (World B
   avatar/inventory) and you arrive inside the entrance room.
2. Confirm no immediate bounce-back and that the World B avatar's movement profile applies.
3. Talk to the Arena Keeper and confirm the Arena Key enters the keyring and cannot be taken twice.
4. Test the arena gate with no key, `golden_key`, and `training_arena_key`.
5. Dash into the closed gate and confirm it blocks; open it and pass through.
6. Cancel the green-box conversation and confirm no spawn.
7. Complete it and confirm exactly one challenger spawns at full HP.
8. Confirm the dash telegraph (yellow ~0.5 s), straight dash, and sword swing.
9. Stand in `world_b_entry` and press **G** to return to `Overworld` at `world_b_portal`.

### Automated coverage

The old Play-mode harness (`TrainingArenaVerification`) has been removed: it required a
`Training Arena Wing` object that no saved scene contains any more, so it could no longer run.
Rebuilding the wing with `Tools > Training Arena > Build Missing Arena` would restore the scene,
but the harness itself is gone.

For automated coverage, use `powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1`
(compile, Edit Mode + Play Mode tests, `dotnet test`, and a scene smoke test). The numbered steps
above remain the manual check.

## Runtime API

```csharp
bool spawned = trainingEnemySpawner.TrySpawn();
NpcController currentEnemy = trainingEnemySpawner.ActiveEnemy;

npcDashMeleeController.SetArenaBounds(new Rect(x, y, width, height));
NpcDashMeleeController.AttackPhase phase = npcDashMeleeController.Phase;
```
