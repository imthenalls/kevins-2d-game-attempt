# Door Placement Brush

## Overview

`DoorPlacementBrush` places the functional 1-, 2-, 3-, and 4-tile sliding-door prefabs from
Unity's Tile Palette. It calculates the cell center automatically and applies the half-cell
offset required by even-length doors. The result remains a normal prefab instance with its
`SlidingDoor`, interaction trigger, sprite, and blocking collider.

## Unity setup

1. Open **Window > 2D > Tile Palette**.
2. In the brush dropdown, choose **Door Placement Brush**.
3. Set **Active Target** to the Grid or Tilemap where the door belongs.
4. In the brush properties, set **Tile Length** from 1 through 4.
5. Leave **Horizontal** off for a vertical door, or enable it for a horizontal door. The Tile
   Palette rotate command also switches orientation.
6. Toggle **Reverse Slide Direction** when the panel needs to open toward the other side.
7. Select the paint tool and click a cell in the Scene view.

Vertical doors slide right by default and left when reversed. Horizontal doors slide down by
default and up when reversed.

The brush creates a `Painted Doors` child beneath the target Grid/Tilemap and places a prefab
instance there. Odd-length doors center directly on the clicked cell. Even-length doors shift
half a cell toward their second occupied cell so their edges stay on grid lines.

The target must use a rectangular Grid and have world scale `(1, 1, 1)`. The brush reports a
warning and does not paint when these requirements are not met.

## Erasing and undo

Choose the Tile Palette eraser and click any occupied cell inside a painted door. The whole door
instance is removed. Painting, container creation, and erasing all support Unity Undo.

## Required prefabs

- `Assets/Prefabs/SlidingDoor_1Tile.prefab`
- `Assets/Prefabs/SlidingDoor_2Tile.prefab`
- `Assets/Prefabs/SlidingDoor_3Tile.prefab`
- `Assets/Prefabs/SlidingDoor_4Tile.prefab`

## Runtime behavior

The brush is editor-only. It does not add runtime dependencies. Every painted instance uses the
existing `SlidingDoor` behavior, including E interaction, optional key requirements, sliding,
and collision changes.
