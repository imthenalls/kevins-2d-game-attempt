# 2D Top-Down Game Basics (Unity)

This game setup is:
1. **2D**
2. **Top-down** (no gravity)

## Core idea
You move the player on the X/Y plane using keyboard input.
Gravity is disabled so the character does not fall.

## Player GameObject requirements
- `SpriteRenderer`
- `Rigidbody2D`
- `Collider2D` (Box/Circle/Capsule)
- `PlayerController2D` script

## Rigidbody2D settings for top-down
- Body Type: `Dynamic`
- Gravity Scale: `0`
- Freeze Rotation Z: enabled (or done by script)

## How movement works
- Read `Horizontal` + `Vertical` input in `Update()`
- Build a 2D direction vector
- Normalize it (so diagonal movement is not faster)
- Apply velocity in `FixedUpdate()`

Formula:
- `velocity = normalizedInput * moveSpeed`

## Scene checklist
- Main Camera set to **Orthographic**
- Player has Rigidbody2D + Collider2D + script
- Walls/obstacles have Collider2D
- Optional: add colliders to map boundaries

## Common issues
- Not moving: script not attached, missing Rigidbody2D, or speed is 0
- Falling: gravity scale is not 0
- Spinning: rotation not frozen
- Fast diagonal movement: input not normalized

## Good next steps
- Add idle/walk animation
- Add interaction key (`E`)
- Add enemies and simple AI
- Add health + UI

---

## Room-to-room movement (no scene reload)

This project now supports room transitions inside one scene.

### Scripts added
- `Room2D`: marks a room, spawn point, and map grid position
- `RoomTransitionTrigger2D`: entrance trigger that sends player to a target room
- `RoomManager2D`: central manager that moves player and activates current room
- `RoomMapUI`: simple minimap/fog-of-war style room map

### Why this is a good approach
- **No full scene reload**: transitions are instant and smooth
- **Simple design**: each room is a GameObject parent with walls/colliders
- **Scalable**: easy to add more rooms and entrances by wiring references
- **Map-ready**: discovered rooms are tracked by the manager

### Setup steps
1. Create each room as a parent GameObject (example: `Room_A`, `Room_B`).
2. Add wall colliders around each room and leave collider gaps for entrances.
3. Add `Room2D` to each room parent.
4. Create an empty child transform for each room spawn point and assign it in `Room2D`.
5. For each entrance, create a trigger collider object and add `RoomTransitionTrigger2D`.
6. In each trigger, assign:
	- `targetRoom` = destination `Room2D`
	- `targetSpawnPoint` = spawn transform in destination room
7. Create one `RoomManager2D` object in scene and assign:
	- `player` transform (tag player as `Player`)
	- optional `startingRoom`
8. Add UI map:
	- Create a Canvas + UI Panel
	- Add `RoomMapUI` to the panel
	- Assign panel RectTransform as `mapRoot`

### Notes
- Keep all rooms in one scene for fast transitions.
- If `keepOnlyCurrentRoomActive` is enabled, only active room stays enabled.
- If you want enemies in visited rooms to keep simulation running, disable that option.

---

## Fixed camera per room (no weird follow movement)

If every room has the same world size, use room-based camera snapping (not player follow).

### What to use
- `Room2D`
	- `cameraAnchor` = room center transform (optional)
	- `roomWorldSize` = room width/height in world units
- `RoomManager2D`
	- `snapCameraToRoomOnEnter` = true
	- `fitOrthographicCameraToRoom` = true
	- `cameraPadding` = small value like `0` to `0.5`

### Result
- On room enter, camera snaps to room center.
- Orthographic size is adjusted to show the full room.
- No scene reload and no drifting camera follow.

### Important
- Main camera should be **Orthographic**.
- If a separate follow-camera script exists (Cinemachine/follow script), disable it to avoid conflicts.

