# Tilemap Rules

## The Rule

Every `Grid` and `Tilemap` GameObject must have:

- Local position `(0, 0, 0)`
- Local rotation identity
- Local scale `(1, 1, 1)`

All placement is expressed through **cell coordinates** only. Never move, rotate, or scale a
Grid or Tilemap transform to position its content.

## Why

- Every tilemap shares one cell space per Grid. If one tilemap has a transform offset, its
  cells no longer line up with any other tilemap, painted prefab, or door/gate placed on the
  grid.
- Tile palette prefabs and scene tilemaps use the same cells. An offset on either side breaks
  alignment even when the cells look identical in the Inspector.
- Isometric rendering multiplies offsets by the 2:1 projection, so a small offset becomes a
  visible gap.

## Doing It Correctly

| Instead of | Do |
|---|---|
| Moving a tilemap transform to shift a room | Shift the tile **cells** by whole numbers |
| Scaling a tilemap to resize tiles | Change `Grid.cellSize` (keep 2:1, e.g. `(1, 0.5)`) |
| Rotating a tilemap | Change `Grid.cellLayout` / `cellSwizzle` |
| Offsetting a tilemap by `(0.42, ...)` to "nudge" art | Fix the tile sprite pivot or the cell coordinates |

When creating tilemaps in code:

```csharp
var mapObject = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
mapObject.transform.SetParent(grid.transform, false); // local position stays (0, 0, 0)
Tilemap map = mapObject.GetComponent<Tilemap>();
map.SetTile(new Vector3Int(x, y, 0), tile);           // placement via cells only
```

`WorldBSceneBuilder` and `TrainingArenaBuilder` already follow this pattern.

## Tools

**Tools > World > Tilemap Alignment**

- **Validate Open Scene** logs every Grid/Tilemap whose transform is not at origin.
- **Normalize Open Scene** bakes a tilemap's local offset into its tile cell coordinates
  (rounding to whole cells), then zeroes the transform. The visual position is preserved to
  within half a cell.

See `Assets/Scripts/World/Editor/TilemapAlignmentTool.cs`.

## History

The pre-isometric scenes carried legacy transform offsets on `Grid/borders` `(0.1, 0.42)` and
`Grid/room 2` `(0.1, -22.46)`, plus a slight non-uniform scale on `room 2`. These were removed
during the isometric conversion; all current scene tilemaps and palette prefabs sit at origin.
