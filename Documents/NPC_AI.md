# NPC AI

Phased NPC AI work. It started with doors, then grew a shared foundation.

- **Phase 1:** entity-based key resolution ([KEY_HOLDER.md](KEY_HOLDER.md)).
- **Phase 2:** door use + locked-gate memory (below).
- **Phase 3:** shared behavior/perception foundation (below).
- **Phase 4:** persisted lock knowledge + grid pathfinding (below).

## Phase 3: Shared Foundation

### NpcBehaviorBase (`Assets/Scripts/NPCs/NpcBehaviorBase.cs`)

An abstract base implementing `INpcBehavior` that all behaviors should derive from. It caches
`Rigidbody2D`, `NpcController`, `NpcPerception`, `NpcMemory`, the `IKeyHolder`, and the facing
`SpriteRenderer`, and provides shared helpers:

| Helper | Purpose |
|---|---|
| `MoveToward(target, speed)` | Drive the Rigidbody2D toward a point and face it |
| `StopMoving()` | Zero velocity |
| `Arrived(target, threshold)` | Distance test |
| `Face(dir)` | Flip the facing sprite |
| `Complete()` | Finish the behavior (manager picks the next) |
| `OnStalled()` | Override to react when the NPC stops making progress |
| `EnsureMemory()` | Get or add `NpcMemory` |

Derived behaviors override `Enter`, `TickBehavior`, and `Exit`. `NpcWanderBehavior` and
`NpcUseDoorBehavior` were refactored onto this base.

### NpcPerception (`Assets/Scripts/NPCs/NpcPerception.cs`)

One shared scan per NPC instead of every behavior running its own physics query. On a fixed
cadence it collects nearby colliders and exposes:

- `Player` — nearest `PlayerController2D` transform.
- `Gates` — distinct `SlidingDoor`s in range.
- `Contacts` — all colliders seen.
- `FindNearestGate(origin, maxDistance)` and `FindNearest&lt;T&gt;(origin, maxDistance)`.

`NpcUseDoorBehavior` uses `FindNearestGate`; when no `NpcPerception` is present it falls back to
a direct search.

## Phase 4: Persistence + Pathfinding

### Persisted lock knowledge

`NpcMemory` can write what it learns into `WorldStateManager`, which `SaveManager` already
serializes, so NPCs remember locked gates across saves and scene reloads.

- Fact key: `Npc.<npcId>.GateLocked.<gateId>` → value is the required key id.
- `SlidingDoor.GateId` is a stable id (serialized `gateId`, falling back to the object name).
- When the NPC later holds the key, `ShouldSkipGate` clears the fact automatically.
- Turn off **Persist** on `NpcMemory` to keep knowledge session-only.

### NpcPathfinder

Grid A* over the isometric `Grid`. Any solid (non-trigger) collider is an obstacle, so walls,
props, and closed gates are avoided automatically.

- `FindPath(start, goal)` returns world waypoints, or null. The gate cell itself may be blocked,
  so the agent paths to it from its own side.
- Walkability uses a **point test at each cell center** — a radius test clips neighboring
  isometric wall diamonds (the cell inradius is only ~0.22).
- `NpcUseDoorBehavior` follows the path when a pathfinder is present; otherwise it walks straight
  at the gate. Set **Obstacle Layers** to the blocking layers.

## Component Summary

### NpcKeyring (`Assets/Scripts/NPCs/NpcKeyring.cs`)

An NPC-owned key inventory implementing `IKeyHolder`. Independent of the NPC's trade inventory.

- **Starting Key Ids**: key item ids the NPC owns on spawn (must match `ItemFlags.KeyItem` items).
- Runtime: `AddKey` / `RemoveKey` / `HasKey` / `CountKey` / `Clear` / `OnKeysChanged`.
- Place it on the NPC root so `SlidingDoor.ResolveKeyHolder` finds it via
  `GetComponentInParent<IKeyHolder>()`.

### NpcMemory (`Assets/Scripts/NPCs/NpcMemory.cs`)

A small per-NPC blackboard. `NpcUseDoorBehavior` adds one automatically if missing.

- `RememberLockedGate(gate, requiredKeyId)`
- `ShouldSkipGate(gate, keys)` — true when the NPC remembers the gate locked **and** still lacks
  the key; automatically forgets it once the key is held.
- `HasLockedMemory`, `ForgetGate`, `Clear`.

### NpcUseDoorBehavior (`Assets/Scripts/NPCs/NpcUseDoorBehavior.cs`)

An `INpcBehavior` for `NpcBehaviorManager` (weighted random selection).

1. On enter, finds the nearest closed `SlidingDoor` within **Detection Radius**. If memory says
   to skip it, the behavior ends immediately.
2. Walks toward the gate (Rigidbody2D velocity), stopping on stall.
3. When within **one tile** of the gate's nearest cell, calls `door.TryUse(gameObject)` and reacts:

| Result | NPC reaction |
|---|---|
| `Opened` | Walks **Pass Through Distance** past the gate, then completes |
| `Locked` | Records it in `NpcMemory`, raises `OnLockedGate` and `QuestEventBus("DoorLocked", keyId)`, then gives up |
| `Busy` | Waits in place and retries next tick |
| `Unavailable` | Gives up |

Because the NPC only tries once per attempt and memory blocks the gate until the key is held, a
locked NPC will not keep pushing the door.

## Setup

1. On the NPC root: `NpcController`, `Rigidbody2D` (Gravity Scale 0, freeze Z), `NpcBehaviorManager`.
2. Add `NpcWanderBehavior` (and any other behaviors) so the door use is one option among several.
3. Add `NpcUseDoorBehavior` and set its **Weight** relative to the others.
4. Add `NpcKeyring` and list any **Starting Key Ids** the NPC should own.
5. `NpcMemory` is added automatically.

To make an NPC open a specific gate, give it the gate's `Required Key Id` in its keyring. To make
it fail and give up, leave the keyring empty.

## Tuning

| Field | Purpose |
|---|---|
| Weight | How often this behavior is chosen vs wander/idle |
| Detection Radius | How far the NPC looks for a gate |
| Move Speed | Walk speed toward the gate |
| Arrival Threshold | Distance treated as "arrived" when walking through after opening |
| Pass Through Distance | How far the NPC continues after the gate opens |

## Events

- `NpcUseDoorBehavior.OnLockedGate(SlidingDoor)` — hook this to play a bark, emote, or head shake.
- `QuestEventBus` event `"DoorLocked"` with the required key id as the target — lets quests or
  analytics react without referencing the behavior.

## Roadmap

- **Phase 3 (done):** `NpcPerception` + `NpcBehaviorBase` shared helpers (targeting, move-to, interact).
- **Phase 4 (done):** persist NPC lock knowledge via WorldState; grid pathfinding (`NpcPathfinder`).
- **Future:** schedules, combat AI, and shared goals.

See [SLIDING_DOORS.md](SLIDING_DOORS.md) and [KEY_HOLDER.md](KEY_HOLDER.md).
