# Script Function Reference

Current scripts and data classes in this project.

---

## PlayerController2D.cs

### Awake()
**Lifecycle Event** - Called once when the component loads.

**Purpose**: Cache the Rigidbody2D and apply top-down physics defaults.

**Details**:
- Gets the player's Rigidbody2D
- Sets gravity to 0 when `forceNoGravity` is true
- Freezes rotation when `lockRotation` is true

---

### Update()
**Lifecycle Event** - Called every frame.

**Purpose**: Read player input and build movement direction.

**Details**:
- Uses Input System keyboard/gamepad when available
- Falls back to legacy Horizontal/Vertical axes otherwise
- Clamps input magnitude to avoid faster diagonal movement

---

### FixedUpdate()
**Lifecycle Event** - Called on physics ticks.

**Purpose**: Apply velocity to move the player.

**Details**:
- Sets `rb.linearVelocity` from `moveInput * moveSpeed`

---

## Portals/PortalTrigger2D.cs

### PortalId (Property)
**Returns**: The trigger's portal ID string used by `PortalManager` mode.

---

### Reset()
**Editor Lifecycle Event** - Called when the component is added/reset.

**Purpose**: Ensure the attached `Collider2D` is configured as a trigger.

---

### OnTriggerEnter2D(Collider2D other)
**Physics Event** - Called when another collider enters this trigger.

**Purpose**: Teleport valid travelers either through manager-config mode or local portal-to-portal mode.

**Details**:
- Validates tag and local cooldown
- Resolves traveler transform from Rigidbody2D when possible
- In manager mode: calls `PortalManager.TryUsePortal(portalId, traveler)`
- In local mode: teleports to destination portal or explicit spawn point
- Optionally auto-separates from destination trigger and resets velocity
- Applies cooldown to both source and destination triggers

---

### BlockForSeconds(float seconds)
**Public Method** - External cooldown helper.

**Purpose**: Prevent immediate retriggering by delaying next valid use.

---

### ExitSideToVector(ExitSide side)
**Private Static Method** - Converts enum side to world-space direction.

**Purpose**: Map `Above/Below/Left/Right` to `Vector3` offsets.

---

### GetFinalExitDistance(Transform traveler)
**Private Method** - Computes effective exit distance.

**Purpose**: Guarantee spawn distance is far enough to avoid overlap with destination trigger.

**Details**:
- Starts from configured `exitDistance`
- If auto-separation is enabled, uses collider extents + padding
- Returns max(configured distance, required non-overlap distance)

---

### GetExtentAlongSide(Collider2D col)
**Private Method** - Returns collider half-size on selected side axis.

**Purpose**: Support overlap-safe spawn distance calculations.

---

## Portals/PortalManager.cs

### Instance (Property)
**Static Singleton** - Global access point for manager-based portal travel.

---

### Awake()
**Lifecycle Event** - Called when the manager loads.

**Purpose**: Initialize singleton, persist across scenes, subscribe to scene-loaded callback, and load portal config.

---

### OnDestroy()
**Lifecycle Event** - Called when the manager is destroyed.

**Purpose**: Unsubscribe callbacks and clear singleton reference safely.

---

### TryUsePortal(string portalId, Transform traveler)
**Public Method** - Main entry point for manager-based teleport requests.

**Purpose**: Validate traveler and portal ID, then perform same-scene teleport or start cross-scene transfer.

**Details**:
- Enforces per-traveler cooldown
- Looks up portal destination by ID
- Teleports immediately when destination scene matches current scene
- Otherwise stores pending destination data and loads destination scene

---

### TryGetPortal(string portalId, out PortalDefinition portal)
**Public Method** - Query helper.

**Purpose**: Read portal definitions from loaded config.

---

### LoadPortalConfig()
**Private Method** - Parses and validates portal JSON.

**Purpose**: Build fast ID lookup dictionary from portal database.

---

### LoadConfigJsonText()
**Private Method** - Config source resolver.

**Purpose**: Load JSON from one of three sources in priority order.

**Order**:
- Inspector `TextAsset`
- `Resources` path
- `StreamingAssets/portals.json`

---

### TeleportTraveler(Transform traveler, PortalDestination destination)
**Private Method** - Performs same-scene teleport.

**Purpose**: Move traveler to destination, optionally rotate, optionally reset velocity.

---

### ResolveDestinationPosition(PortalDestination destination)
**Private Method** - Computes final destination position.

**Purpose**: Resolve destination in priority order.

**Order**:
- Destination portal ID + side/distance offset
- Named spawn point
- Raw fallback position from JSON

---

### OnSceneLoaded(Scene scene, LoadSceneMode mode)
**Private Callback** - Called by Unity after scene load.

**Purpose**: Complete pending cross-scene teleport and apply pending settings.

---

### MarkTravelerCooldown(int travelerId)
**Private Method** - Stores next valid portal time for traveler.

**Purpose**: Prevent rapid repeated teleports.

---

### ClearPendingDestination()
**Private Method** - Clears pending cross-scene transfer state.

**Purpose**: Reset manager pending fields after transfer completes/fails.

---

### TryResolvePortalExitPosition(string portalId, string exitSide, float exitDistance, out Vector3 targetPosition)
**Private Method** - Looks up a `PortalTrigger2D` by ID and computes portal-relative exit position.

**Purpose**: Support JSON destinations that spawn outside a destination portal.

---

### ExitSideToVector(string exitSide)
**Private Static Method** - String-to-direction mapper.

**Purpose**: Convert JSON side strings (`above`, `below`, `left`, `right`, etc.) into a `Vector3` direction.

---

## Portals/PortalSpawnPoint.cs

### SpawnId (Property)
**Returns**: Named spawn ID for manager lookups.

**Purpose**: Mark destination points in-scene for portal arrivals.

---

## Portals/PortalData.cs

These are serializable data classes used by JSON parsing in `PortalManager`.

### PortalDatabaseJson
**Fields**:
- `version`
- `List<PortalDefinition> portals`

---

### PortalDefinition
**Fields**:
- `id`
- `PortalDestination destination`
- `List<PortalMetadataEntry> metadata`

---

### PortalDestination
**Fields**:
- `scene`
- `useSpawnPoint`
- `spawnId`
- `usePortalExitOffset`
- `destinationPortalId`
- `exitSide`
- `exitDistance`
- `position`
- `rotationEuler`

---

### PortalMetadataEntry
**Fields**:
- `key`
- `value`

---

### SerializableVector3

#### SerializableVector3()
**Constructor** - Default empty constructor for Unity JSON serialization.

#### SerializableVector3(float x, float y, float z)
**Constructor** - Convenience constructor.

#### ToVector3()
**Method** - Converts serializable struct-style values to Unity `Vector3`.
