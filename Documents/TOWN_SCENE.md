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
