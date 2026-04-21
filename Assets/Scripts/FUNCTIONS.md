# Script Function Reference

Complete documentation of all functions in each script.

---

## PlayerController2D.cs

### Awake()
**Lifecycle Event** - Called once when the script instance is being loaded.

**Purpose**: Initialize the player's Rigidbody2D component and apply physics settings.

**Details**:
- Retrieves the `Rigidbody2D` attached to the player object
- If `forceNoGravity` is enabled, sets `gravityScale` to 0 (disables gravity)
- If `lockRotation` is enabled, freezes rotation on the Z-axis to prevent the player from spinning

**When it runs**: Earliest in the GameObject lifecycle, before Start()

---

### Update()
**Lifecycle Event** - Called once per frame before physics calculations.

**Purpose**: Read player input and build a movement vector.

**Details**:
- Checks if Input System package is enabled
- If enabled: reads WASD keys and arrow keys from `Keyboard.current`, also reads gamepad left stick
- If disabled: falls back to legacy Input Manager axes ("Horizontal" and "Vertical")
- Clamps the final input magnitude to 1.0 to prevent diagonal movement from being faster
- Stores result in `moveInput` for FixedUpdate to use

**When it runs**: Every frame, before FixedUpdate

---

### FixedUpdate()
**Lifecycle Event** - Called once per physics timestep.

**Purpose**: Apply movement velocity to the Rigidbody2D.

**Details**:
- Multiplies the normalized `moveInput` by `moveSpeed`
- Sets the Rigidbody2D's `linearVelocity` to move the player
- Physics engine then moves the GameObject based on this velocity

**When it runs**: At fixed timesteps (default 50 times per second, independent of frame rate)

---

## Room2D.cs

This is a data container with no functions—only properties.

### RoomId (Property)
**Returns**: The unique identifier string for this room.

---

### DefaultSpawnPoint (Property)
**Returns**: The spawn point transform for this room, or the room's own transform if not assigned.

---

### MapGridPosition (Property)
**Returns**: The X/Y grid coordinates for this room on the minimap.

---

### CameraCenter (Property)
**Returns**: The world position where the camera should focus, or the room transform position if no anchor assigned.

---

### RoomWorldSize (Property)
**Returns**: The width and height of this room in world units.

---

## RoomTransitionTrigger2D.cs

### Reset()
**Editor Lifecycle Event** - Called when the component is first added or "Reset" is clicked in the Inspector.

**Purpose**: Automatically configure the trigger collider.

**Details**:
- Retrieves the Collider2D component on this GameObject
- Sets `isTrigger = true` so it acts as a trigger volume, not a solid collider

**When it runs**: Only in the editor, once when component is initialized

---

### OnTriggerEnter2D(Collider2D other)
**Physics Event** - Called when another 2D collider enters this trigger.

**Purpose**: Detect when the player enters the trigger zone and teleport them to the target room.

**Details**:
- Returns early if `targetRoom` is not assigned
- Checks if the colliding object has the "Player" tag to ensure only the player triggers the transition
- Gets the `RoomManager2D` singleton instance
- Calls `TryTransitionTo()` with the target room and spawn point
- Returns early if any check fails (no room, wrong tag, no manager)

**When it runs**: Physics event, triggered when collision/overlap begins

---

## RoomManager2D.cs

### Awake()
**Lifecycle Event** - Called once when the script instance is being loaded.

**Purpose**: Set up the singleton instance and find all rooms in the scene.

**Details**:
- Implements the singleton pattern: if an instance already exists, destroys this one
- If this is the first instance, stores it as `Instance`
- Calls `CacheRooms()` to find all `Room2D` components in the scene

**When it runs**: Earliest in the GameObject lifecycle

---

### Start()
**Lifecycle Event** - Called once, before the first Update() and after all Awake() calls.

**Purpose**: Initialize the player reference and enter the starting room.

**Details**:
- If `player` is not assigned in Inspector, searches for a GameObject with the "Player" tag
- Determines the initial room: uses `startingRoom` if set, otherwise checks if player is already in a room, otherwise finds nearest room
- Calls `EnterRoom()` with the initial room and its default spawn point (without moving the player initially)
- Applies player depth safety settings

**When it runs**: Once at game start, after Awake()

---

### TryTransitionTo(Room2D targetRoom, Transform targetSpawnPoint = null)
**Public Method** - Called externally (e.g., by RoomTransitionTrigger2D) to attempt a room transition.

**Purpose**: Safely transition to a new room with cooldown protection.

**Details**:
- Returns early if no target room or no player
- Checks cooldown timer: prevents transitions from happening faster than `transitionCooldown` seconds
- If cooldown is satisfied, calls `EnterRoom()` with movePlayer = true to teleport the player
- Safely blocks rapid/spam transitions that could cause issues

**When it runs**: When player touches a transition trigger

---

### IsDiscovered(Room2D room)
**Public Method** - Query whether a room has been visited.

**Purpose**: Check discovery status for UI/map purposes.

**Details**:
- Returns true if the room is in the `discoveredRooms` HashSet
- Returns false if room is null or hasn't been visited
- Used by RoomMapUI to color undiscovered vs discovered rooms differently

**When it runs**: Called by RoomMapUI when updating map colors

---

### EnterRoom(Room2D room, Transform spawnPoint, bool movePlayer)
**Private Method** - Core room transition logic.

**Purpose**: Move the player to a new room and update game state.

**Details**:
- Sets `CurrentRoom` to the new room
- Adds the room to `discoveredRooms` HashSet
- If `keepOnlyCurrentRoomActive` is enabled:
  - Deactivates all other rooms
  - Keeps current room and any rooms containing protected objects (player, manager) active
- If `movePlayer` is true:
  - Teleports player to the spawn point position
  - Applies depth safety (sets player Z to playerZ if `enforce2DDepth` is true)
  - Clears the player's velocity to stop momentum
- Calls `ApplyPlayerDepth()` again for safety
- Records the transition time for cooldown checking
- Fires the `RoomChanged` event for listeners (like RoomMapUI)

**When it runs**: During room transitions

---

### CacheRooms()
**Private Method** - Find and cache all Room2D instances.

**Purpose**: Build a list of all rooms in the scene for fast lookup.

**Details**:
- Uses `FindObjectsOfType<Room2D>(true)` to find all rooms (including inactive ones)
- Stores result in `allRooms` array for quick access without repeated searches

**When it runs**: Once in Awake()

---

### FindNearestRoomToPlayer()
**Private Method** - Locate the room closest to the player's current position.

**Purpose**: Fallback room assignment when no starting room is specified.

**Details**:
- Iterates through all cached rooms
- Calculates squared distance from each room to the player (squared for performance)
- Returns the room with the smallest distance
- Returns first room if list is empty

**When it runs**: At startup if no starting room assigned

---

### GetRoomContainingPlayer()
**Private Method** - Check if player is already a child of any Room2D.

**Purpose**: Determine if player starts inside an existing room.

**Details**:
- Uses `GetComponentInParent<Room2D>()` to check if player is nested under a Room2D
- Returns null if player is not a child of any room

**When it runs**: At startup for room assignment priority

---

### ContainsProtectedObjects(Room2D candidate)
**Private Method** - Check if a room contains objects that should stay active.

**Purpose**: Prevent deactivating rooms with essential objects (player, manager).

**Details**:
- Checks if player is a child of the candidate room
- Checks if the RoomManager2D itself is a child of the candidate room
- Returns true if any protected object is found
- Used to keep rooms active even when they're not the current room

**When it runs**: When deciding which rooms to deactivate

---

### ApplyPlayerDepth()
**Private Method** - Enforce consistent player Z position.

**Purpose**: Keep player visible on the correct rendering plane in 2D.

**Details**:
- Only runs if `enforce2DDepth` is enabled
- Sets player's Z position to `playerZ` value (typically 0)
- Ensures player doesn't slip into background or become invisible

**When it runs**: On room entry and at game start

---

## RoomMapUI.cs

### Awake()
**Lifecycle Event** - Called once when the script instance is being loaded.

**Purpose**: Find the RoomManager2D if not assigned.

**Details**:
- If `roomManager` is not set in Inspector, gets the singleton instance from `RoomManager2D.Instance`

**When it runs**: Early in lifecycle

---

### OnEnable()
**Lifecycle Event** - Called when the GameObject or component becomes enabled.

**Purpose**: Subscribe to room changes and build the initial map.

**Details**:
- Registers the `HandleRoomChanged` callback to the RoomManager2D's `RoomChanged` event
- Calls `BuildMap()` to create UI tiles for all rooms
- Calls `RefreshMapColors()` to set initial tile colors based on discovery state

**When it runs**: When the UI element is shown/enabled

---

### OnDisable()
**Lifecycle Event** - Called when the GameObject or component becomes disabled.

**Purpose**: Clean up event subscription.

**Details**:
- Unsubscribes `HandleRoomChanged` from `RoomChanged` event
- Prevents memory leaks and dangling event handlers

**When it runs**: When the UI element is hidden/disabled

---

### HandleRoomChanged(Room2D _)
**Event Callback** - Triggered when RoomManager2D fires the RoomChanged event.

**Purpose**: Update map colors when player enters a new room.

**Details**:
- Calls `RefreshMapColors()` to recolor tiles
- The parameter `_` is ignored (it's the new room, but we don't need it)

**When it runs**: Every time player transitions to a new room

---

### BuildMap()
**Private Method** - Create UI tiles for all rooms.

**Purpose**: Dynamically generate the minimap UI.

**Details**:
- Destroys any existing tiles in mapRoot
- Finds all `Room2D` components in the scene
- For each room:
  - Creates a UI GameObject with an Image component
  - Positions it at `mapGridPosition * cellSize` (grid-based layout)
  - Sets initial color to `undiscoveredColor` (dark)
  - Stores reference in `roomTiles` Dictionary for quick lookup

**When it runs**: When UI first enables and whenever map needs rebuilding

---

### RefreshMapColors()
**Private Method** - Update tile colors based on discovery state.

**Purpose**: Show current room in green, discovered rooms in gray, undiscovered in dark.

**Details**:
- Iterates through all room tiles in the `roomTiles` Dictionary
- For each tile:
  - If it's the current room: color it `currentRoomColor` (bright green)
  - Else if discovered: color it `discoveredColor` (light gray)
  - Else: color it `undiscoveredColor` (dark gray)

**When it runs**: When player enters a room or map is refreshed

---

## Summary

- **PlayerController2D**: Handles input reading and applies movement velocity each frame
- **Room2D**: Data container that stores room metadata (spawn, map position, size)
- **RoomTransitionTrigger2D**: Detects player collision with exit trigger and requests room change
- **RoomManager2D**: Central orchestrator that manages room state, transitions, and discovery tracking
- **RoomMapUI**: Visualizes the room map based on discovery state, updates when rooms are entered
