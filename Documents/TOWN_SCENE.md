# Town Scene

A placeholder town map built entirely from coloured tiles (no art assets): the Mega-Man-style
overhead block layout, used as a sandbox for movement, collision, and NPC pathfinding.

## Running it

1. **Tools > Worlds > Create Town Scene** — builds `Assets/Scenes/Town.unity` and adds it to Build
   Settings. It refuses if the scene already exists; delete it (or use an eval) to rebuild.
2. Open `Assets/Scenes/Town.unity` and press Play.

A colour-mockup of the design (rendered before the scene existed) is
`Documents/Concept_Town.png`.

## Layout

- Isometric Grid, cell `(1, 0.5)`, `40 x 28` cells.
- **Grass** (green) fills the map; **Streets** (dark gray) form a 3-cell cross
  (vertical `cx 18–20`, horizontal `cy 12–14`), giving four town blocks.
- **Nine buildings** — each is a rectangle of building tiles (light gray) with **one pink entrance**.
- **Park** in the south-east block: grass with a 3×3 red plaza.

## Tiles

Generated as assets under `Assets/tiles/Town/` from the default square sprite, each tinted:

| Tile | Collider | Used for |
|---|---|---|
| `GrassTile` | none | ground |
| `StreetTile` | none | roads |
| `BuildingTile` | Grid | building footprints (**on the `Walls` layer**) |
| `EntranceTile` | none | the single passable cell of each building |
| `PlazaTile` | none | park centre |

## Buildings are solid; only the entrance is passable

The `Buildings` tilemap carries a `TilemapCollider2D` and sits on the **`Walls`** layer. Because the
NPC pathfinder's `obstacleLayers` is `~(1 << Npc)` (see
[NPC_STRESS_TEST.md](NPC_STRESS_TEST.md)), the building tiles are obstacles to player collision *and*
NPC pathfinding. The entrance cell is simply **not painted** on the `Buildings` tilemap (it gets the
non-colliding `EntranceTile` instead), so it is the only way through — real colliders, real
pathfinding, no art.

## Gotcha worth knowing

A `Tilemap` created in the same editor tick as its `Grid` silently ignores `SetTile` — the scene
saves empty. `TownSceneBuilder` therefore creates the scene, then paints on the next editor tick
(`FinishPaint`, also callable explicitly), and calls `RefreshAllTiles()` on every map before saving.
If you write another tilemap builder, do the same or verify with `HasTile` after saving.

## Verified

`Tools/verify-all.ps1` opens Town, enters Play, and fails on any console error. The scene is covered
by the scene smoke test alongside Overworld and WorldB.
