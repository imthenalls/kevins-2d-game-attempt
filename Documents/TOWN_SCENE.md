# Town Scene

A placeholder town map built with **no art assets**: coloured ground tiles plus building
**GameObjects with real colliders** (like NPCs or the training spawn box) — not building tiles.

## Running it

1. **Tools > Worlds > Create Town Scene** — builds `Assets/Scenes/Town.unity` and adds it to Build
   Settings. It refuses if the scene exists; delete it to rebuild.
2. Open `Assets/Scenes/Town.unity` and press Play.

A colour mockup of the design is `Documents/Concept_Town.png`.

## Layout

- Isometric Grid, cell `(1, 0.5)`, **64 × 44** cells.
- Ground is tiles: **grass** (green) everywhere, **streets** (dark gray) in a 3-wide cross
  (vertical `cx 30–32`, horizontal `cy 20–22`) forming four blocks, and a **park** in the south-east
  block with a 3×3 **red plaza**.
- **Fourteen buildings**, small blocks (4x3 cells) in tidy rows, spaced so their footprints never touch.

## Buildings: solid overworld props with a teleporting door

Buildings are **not** walk-in and **not** tiles. Classic RPG / Pokémon style: the building is an
overworld prop and the door teleports you elsewhere (an interior scene) where the inside is rendered.

Each building is a GameObject (`Building_<x>_<y>`) with:

- **one solid `BoxCollider2D`** covering the whole footprint, on the **`Walls`** layer — no gap, the
  player never walks inside. Because it is on `Walls` and the NPC pathfinder's `obstacleLayers` is
  `~(1 << Npc)` (see [NPC_STRESS_TEST.md](NPC_STRESS_TEST.md)), it blocks the player *and* NPC
  pathfinding, exactly like an NPC or the training spawn box;
- a square `SpriteRenderer` body;
- a pink **`Door`** child: a `BoxCollider2D` with `isTrigger` **plus a `PortalTrigger2D`**, i.e. it
  reuses the existing portal teleport machinery (same as world portals), with a unique id
  (`door_<x>_<y>`).

**The door destinations are intentionally blank.** Fill in `destinationScene` / `destinationPortalId`
on each `PortalTrigger2D` once the interior scenes and their arrival portals exist. The scene also
contains a `Town Portal Manager` so the doors have a `PortalManager` to route through.

## Tile assets

Ground tiles are generated under `Assets/tiles/Town/` from the default square sprite:
`GrassTile`, `StreetTile`, `PlazaTile` (all non-colliding). Buildings and entrances are **not** tiles.

## Gotcha worth knowing

A `Tilemap` created in the same editor tick as its `Grid` silently ignores `SetTile` — the scene
saves empty. `TownSceneBuilder` therefore creates the scene, then paints on the next editor tick
(`FinishPaint`, also callable directly) and calls `RefreshAllTiles()` before saving. If you write
another tilemap builder, do the same and verify with `HasTile` after saving.

## Verified

`Tools/verify-all.ps1` opens Town, enters Play, and fails on any console error; the scene is covered
by the smoke test alongside Overworld and WorldB.

## Ring road and town NPCs

The streets are a **cross** (vertical `cx 30-32`, horizontal `cy 20-22`) plus a **3-cell ring road
around the whole perimeter** (`cx < 3`, `cy < 3`, `cx >= GridW-3`, `cy >= GridH-3`), joined to the
cross. Streets: 891 tiles.

**8 placeholder NPCs** (`town_npc_1..8`, `Npc` layer) spawn on the ring and wander the town:
`NpcController` + `NpcBehaviorManager` + `NpcWanderBehavior` (pathfinding, `WanderRadius 10`) +
`NpcPathfinder` (`obstacleLayers = ~(1<<Npc)`) + `NpcPerception` + `Rigidbody2D`/`CircleCollider2D`,
so they path around the buildings (which are solid on the `Walls` layer).

## Perimeter walls, spawn point, player

- **Perimeter walls**: a `Walls` Tilemap (on the `Walls` layer, `TilemapCollider2D`) is painted one cell
  outside the roads, so town NPCs cannot wander off the map.
- **`Player Spawn`** (`PlayerSpawnPoint`) is created by the builder at cell `(10, 10)`.
- A **Town Player** (`WorldAPlayer` prefab) is placed at the spawn, so the scene satisfies the scene
  requirements (a spawn point needs a `PlayerController2D` to spawn into).
