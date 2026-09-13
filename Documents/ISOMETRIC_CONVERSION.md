# Isometric Conversion

The game's presentation is being converted from flat top-down 2D to a **Mega Man Battle
Network (MMBN 1-4) style isometric view**: a fixed, non-rotating camera looking at a 2:1
diamond panel floor, with upright 2D sprites that never tilt.

This began as a proof of concept on the `Overworld` scene (previously `NewScene`). The
pattern is intended to be applied to the training wing builders and `WorldB` after review.

## Design Rules

- **Camera is never rotated.** The diamond comes from the tilemap projection, not the camera.
  Rotating the camera would tilt the upright sprites, which is not the MMBN look.
- **Sprites stay axis-aligned.** Characters are billboarded squares/circles. Facing is handled
  by flip/rotation on the visual child, which is invisible on placeholder shapes.
- **Movement stays screen-space.** With an unrotated camera and an isometric tilemap, WASD
  maps directly to the 8 screen directions, which is exactly how MMBN movement works. No input
  remap is required.
- **Depth is sorted by world Y** so objects lower on screen draw in front.

## Projection

Unity's `Grid` uses `CellLayout.Isometric` with cell size `(1, 0.5)` (2:1 dimetric). A cell
`(x, y)` maps to world space as:

```
world = ( (x - y) * 0.5 , (x + y) * 0.25 )
```

The same linear transform is applied to every world-space (non-UI) object position when a
scene is converted, so the whole level rotates into a diamond while relative layout is
preserved.

## What Was Done To `Overworld`

1. **Renamed** `Assets/Scenes/NewScene.unity` to `Assets/Scenes/Overworld.unity`. The scene
   GUID is unchanged, so all GUID references survive. Name references were updated in:
   `EditorBuildSettings.asset`, `WorldB.unity` (portal route), `PlayerAvatarPrefabCreator.cs`,
   `WorldBSceneBuilder.cs`, `TrainingArenaBuilder.cs`, `TrainingArenaVerification.cs`.
2. **Both Grids** (`Grid`, `Training Arena Wing/Training Grid`) set to Isometric, cell size
   `(1, 0.5)`.
3. **All non-UI object world positions** transformed by the projection above (47 transforms).
4. **Placeholder diamond art generated**:
   - `Assets/Sprites/Isometric/FloorDiamond.png` - blue panel with a cyan rim.
   - `Assets/Sprites/Isometric/WallDiamond.png` - light grey wall diamond.
   - `Assets/Tiles/Isometric/FloorDiamond.asset` (`Collider Type = None`).
   - `Assets/Tiles/Isometric/WallDiamond.asset` (`Collider Type = Grid`).
5. **Existing tilemaps repainted** with the diamond tiles: `borders`, `room 2`, and the training
   `Walls` become wall diamonds; the training floor becomes floor diamonds.
6. **Panel floors added** (`borders Floor`, `room 2 Floor`) by flood-filling the empty interior
   of each wall tilemap with floor diamonds. These have no collider.
7. **Camera** left orthographic and unrotated; `Transparency Sort Mode = Custom Axis` with axis
   `(0, 1, 0)`. The camera remains a child of `Player`, so it follows the player at a fixed angle.

## Applying The Pattern To Other Scenes

Run the conversion editor script (`Assets/Editor/BridgeScratch.cs`) from the Unity Bridge on the
target scene, or use `Tools > Training Arena > Build Missing Arena` style builders after updating
them to isometric. The reusable steps are:

1. Ensure the scene's `Grid` uses `Isometric` / `(1, 0.5)`.
2. Load `Assets/Tiles/Isometric/FloorDiamond.asset` and `WallDiamond.asset`.
3. Repaint wall tilemaps with the wall tile and floor tilemaps with the floor tile.
4. Apply the projection to all non-UI transforms.
5. Set the camera's transparency sort mode to `CustomAxis (0, 1, 0)`.

## Tuning

| Item | Where |
|---|---|
| Panel colors | Regenerate the PNGs in `Assets/Sprites/Isometric/` |
| Tile scale | `Grid.cellSize` (keep 2:1, e.g. `(1, 0.5)`) |
| Camera framing | `Player/Main Camera` orthographic size |
| Facing behavior | `PlayerController2D` (`faceMovementDirection`, `spriteForwardAngle`) |

## Known Limitations (Proof Of Concept)

- The main-area panel floor flood-fills the connected empty region, so it can extend past room
  walls where the wall tilemap has gaps. Room-accurate floors need authored boundaries.
- Standalone `BoxCollider2D` walls (for example the sliding-door hallway walls) move to diamond
  positions but remain rectangular, so their shape does not match the diamond tiles.
- Doors are grid-cell gates: per-cell diamond sprites snapped with `Grid.GetCellCenterWorld`;
  see [SLIDING_DOORS.md](SLIDING_DOORS.md).
- NPC wander/melee ranges still use world X/Y distances; visually these are now diagonal.
- Only `Overworld` is converted. `WorldB` and generated scenes remain rectangular until the
  pattern is applied there.
- Collision uses diamond `Grid`-collider tiles for painted walls; diagonal movement into corners
  may feel slightly different from the old rectangular layout.

## Rollback

The scene is tracked in git. Restore `Assets/Scenes/Overworld.unity` (or the old
`NewScene.unity`) and delete `Assets/Sprites/Isometric`, `Assets/Tiles/Isometric`,
`Assets/Scenes/Overworld.unity*` to fully revert.
