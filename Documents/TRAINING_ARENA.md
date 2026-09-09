# Training Arena Wing

## Overview

`NewScene` contains a portal-linked training wing built from the project's existing square
tile and default Unity placeholder sprites. It adds a small entrance room, a hallway with a
locked sliding door, and a larger combat room. No original artwork, generated artwork, or
final character design is included.

The route is:

```text
Existing NPC room
  <-> Hub Training Portal / Small Room Return Portal
Small entrance room
  <-> hallway
Training Arena Locked Door (requires Arena Key)
  <-> large combat room
Green Training Box -> repeatable Training Challenger
```

## Scene hierarchy

The scene-owned objects are grouped under `Training Arena Wing`:

```text
Training Arena Wing
  Training Grid
    Floor - small room, hallway, arena
      Tilemap
      TilemapRenderer
    Walls
      Tilemap
      TilemapRenderer
      TilemapCollider2D
  Hub Training Portal
    SpriteRenderer
    BoxCollider2D (trigger)
    PortalTrigger2D
    ExitPoint
  Small Room Return Portal
    SpriteRenderer
    BoxCollider2D (trigger)
    PortalTrigger2D
    ExitPoint
  Arena Key Keeper
    SpriteRenderer
    BoxCollider2D
    NpcController
    NpcDialogue
  Training Arena Locked Door
    SlidingDoor
    interaction trigger
    DoorPanel
      SpriteRenderer
      BoxCollider2D
  Green Training Box
    SpriteRenderer
    BoxCollider2D
    TrainingEnemySpawner
  Challenger Spawn Point
```

The player keeps `CombatReceiver.Invincible` enabled. Its `Rigidbody2D` uses continuous
collision detection so the player dash cannot skip through the closed arena door.

## Rooms and hallway

The floor and walls use `Assets/Settings/squares.asset`, the same tile asset already used by
the project. The wall tilemap has a `TilemapCollider2D`; every outer floor edge is enclosed.
The small room connects to the large arena only through the hallway and its locked door.

Current world-space areas:

| Area | Bounds or position |
|---|---|
| Small room floor | x 44–51, y -4–3 |
| Hallway floor | x 52–60, y -1–1 |
| Arena floor | x 61–78, y -7–6 |
| Locked door | `(56.5, 0.5)` |
| Green box | `(64, 4)` |
| Enemy spawn point | `(70, 0)` |

These visuals are placeholders. Replace their sprites, colors, and tiles with human-made art
without changing the gameplay components or stable IDs.

## Portal connection

The portal pair uses the existing same-scene `PortalManager` flow:

| GameObject | Portal ID | Destination | Position |
|---|---|---|---|
| Hub Training Portal | `training_hub` | `training_entry` | `(6, 4)` |
| Small Room Return Portal | `training_entry` | `training_hub` | `(46, 0)` |

Each portal has an `ExitPoint` outside its trigger to prevent immediate return travel. The
generated route list is recorded in `Documents/Generated/portal-map.md` and
`Documents/Generated/portal-map.json`.

## Arena Key Keeper

`Arena Key Keeper` is placed with the NPCs in the existing room. Its setup is:

| Field | Value |
|---|---|
| `NpcController.Npc Id` | `training_key_keeper` |
| `NpcController.Display Name` | `Arena Keeper` |
| `NpcDialogue.Dialogue Id` | `training_arena_key_gift` |
| `NpcDialogue.Gift Item Id` | `training_arena_key` |
| Gift quantity | `1` |

`Assets/StreamingAssets/items.json` defines `training_arena_key` as a unique `KeyItem`.
`Assets/StreamingAssets/npc_inventories.json` gives one copy to the keeper. Completing the
conversation transfers that owned key into `PlayerKeyring`. Because the item is unique and
the NPC no longer owns it after transfer, repeating the dialogue cannot duplicate the key.

## Locked door

`Training Arena Locked Door` is an instance of `Assets/Prefabs/SlidingDoor.prefab`. Its
`requiredKeyId` override is `training_arena_key`; the existing `golden_key` does not satisfy
this lock. The door remains unlocked after the correct key opens it and does not consume the
key. The panel is rotated and scaled to cover the three-tile-high hallway opening.

## Green Training Box

`TrainingEnemySpawner` implements `IInteractable`, so the existing player interaction flow
drives it. Put it on a layer included by
`PlayerInteractionController.Interactable Layers` if scene layers are changed later.

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
end of the warning and does not steer during the dash. The controller casts the enemy's full
body collider before every movement step, uses continuous collision detection, and clamps
movement to `Arena Bounds`. Solid walls stop the dash. Leaving the arena or killing/disabling
the enemy clears velocity, warning color, and attack state.

Key `NpcDashMeleeController` fields:

| Field | Default | Purpose |
|---|---:|---|
| Warning Duration | 0.5 s | Time between yellow telegraph and dash |
| Warning Color | Yellow | Temporary body color during telegraph |
| Approach Speed | 2.8 | Movement before the telegraph |
| Dash Range | 6 | Maximum committed dash distance |
| Dash Speed | 14 | Dash movement speed |
| Stopping Distance | 1.1 | Space reserved for the sword swing |
| Recovery Duration | 1.1 s | Delay before the next attack cycle |
| Obstacle Layers | All by default | Solid layers that stop movement |
| Arena Bounds | Scene-specific | Limits pursuit and movement to the room |

## Manual verification

No Unity UI automation should be used for this project. Verify the wing manually in Play mode:

1. Confirm the player is invincible and can still be targeted and struck by enemies.
2. Enter `training_hub`; confirm arrival inside the small room and no immediate bounce-back.
3. Return through `training_entry` and speak to the Arena Keeper.
4. Confirm the Arena Key appears in the keyring and cannot be received twice.
5. Test the arena door with no key, `golden_key`, and `training_arena_key`.
6. Dash into the closed door and confirm it blocks the player; open it and pass through.
7. Cancel the green-box conversation and confirm no spawn.
8. Complete the conversation and confirm exactly one challenger spawns at full HP.
9. Confirm the enemy stops, turns yellow for about 0.5 seconds, then dashes straight and
   swings its sword.
10. Dodge sideways during the warning and confirm the dash does not turn toward the new position.
11. Lure the enemy toward each wall and confirm its full collider stops before the wall.
12. Kill it with the player's sword, then spawn and kill several additional rounds.
13. Kill it during the yellow warning and confirm no delayed dash or swing occurs.

## Runtime API

```csharp
bool spawned = trainingEnemySpawner.TrySpawn();
NpcController currentEnemy = trainingEnemySpawner.ActiveEnemy;

npcDashMeleeController.SetArenaBounds(new Rect(x, y, width, height));
NpcDashMeleeController.AttackPhase phase = npcDashMeleeController.Phase;
```
