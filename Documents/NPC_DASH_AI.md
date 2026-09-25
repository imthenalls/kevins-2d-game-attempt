# NPC Dash-Melee AI

## Overview

The dash-melee enemy is a telegraph-dash attacker: it walks toward the player, flashes a warning,
dashes in a committed straight line, swings, then recovers before repeating. The phase machine is
engine-free (`NpcDashMeleeModel` in `Game.Data`) and shared by both presentations:

- **2D** — `NpcDashMeleeController` (used in the training arena).
- **3D** — `NpcDashMelee3D` (used by the Town `Dasher`).

The committed dash is a deliberate design: the dash direction is fixed at the end of the warning and
does not steer afterward, so the player can dodge after seeing the telegraph.

## State machine

| Phase | Behavior |
|---|---|
| `Approach` | Walks toward the player, navigating around obstacles. |
| `Warning` | Body tints yellow for `WarningDuration`; a clear shot is re-checked before committing. |
| `Dash` | Moves `DashRemaining = distance - StoppingDistance` in the committed straight direction. |
| `Swing` | Opens the `CombatAttacker` hit window once. |
| `Recovery` | Stands still for `RecoveryDuration`, then returns to `Approach`. |

`NpcDashMeleeModel.Tick(...)` returns an `NpcDashDecision` (phase, intent, distance, direction), and
the adapter applies movement/attack. `ReportDashMoved(moved, desired)` feeds wall-blocked results
back into the model.

## Components

| Component | Layer | Role |
|---|---|---|
| `NpcDashMeleeModel` | Game.Data | Phase machine + dash commit/recovery decisions. |
| `NpcDashMeleeConfig` | Game.Data | Tuning (durations, speeds, warning color, arena bounds). |
| `NpcDashMeleeController` / `NpcDashMelee3D` | Presentation | Samples the world (distance/direction, wall casts, LOS), applies movement, drives the attacker. |
| `NpcChaseNavigator` | Presentation | Routes the `Approach` step around buildings via `NpcPathfinder3D` → `GridPathfinder`. |

## Unity Setup (3D Town)

1. Add `NpcController` (`NpcType.Enemy`), a `Rigidbody` (no gravity, Y frozen), and a `CapsuleCollider`.
2. Add `NpcDashMelee3D`; assign Body Collider and Body Renderer for the warning tint.
3. Add `NpcPathfinder3D` (obstacle layers = Walls) and `NpcChaseNavigator` so the approach routes.
4. Add a weapon rig (`CombatAttacker` with Use Player Input off, plus `EquippedWeaponVisual3D`).

## Known issues and fixes

These were fixed together; each is retained here so the reasoning is not lost if any regresses.

### 1. The dasher damaged itself when it swung

**Symptom:** the dasher's own HP dropped when it attacked.

**Root cause:** `CombatAttacker` captured its own `CombatReceiver` in `Awake`, but enemy receivers
are added by `NpcController.Awake`, which can run *after* `CombatAttacker.Awake`. The cached
`_selfReceiver` was therefore null, so `AttackModel.TryRegisterHit(receiver, CanHitSelf=false, self=null)`
compared the receiver against `null` and let the self-hit through.

**Fix (`CombatAttacker.TryApplyWeaponHit`):** reject any receiver whose transform is a child of the
attacker's own transform, and resolve the self receiver lazily (`GetComponent<CombatReceiver>()`
whenever the cached reference is null). Both 2D and 3D weapons call this path, so one fix covers both.

### 2. The dasher looped `Warning → Dash → Swing` into a building

**Symptom:** with the player behind a building, the dasher dashed into the wall, recovered, and
re-dashed forever.

**Root cause:** the model entered `Warning` whenever within `DashRange`, regardless of whether a
wall was in the way; the line-of-sight check also used a thin center ray that read wall corners as
"clear".

**Fix:**
- `NpcDashMeleeModel` now takes a `hasLineOfSight` flag and only commits a dash when clear; it aborts
  the `Warning` back to `Approach` if sight is lost mid-telegraph.
- A wall-blocked dash returns to `Approach` (reposition) instead of `Swing` (attacking the wall).

### 3. Chasers (dasher, brute, bandits) jammed against a wall when close but obstructed

**Symptom:** within ~2.5 units of the player but separated by a wall, an enemy pressed straight into
it with zero velocity.

**Root cause:** `NpcChaseNavigator.TryGetStepDirection` returned a *direct* step whenever
`distance <= directRange`, without checking line of sight.

**Fix:** the navigator drives straight only when it has a genuinely clear line (body-width sphere
cast) or is beyond the pathable range; the `directRange` short-circuit was removed.

### 4. Line-of-sight used a thin ray, so corners read as clear

**Symptom:** an enemy with a clear center line still clipped a wall corner on a committed dash.

**Fix:** `NpcDashMelee3D.HasLineOfSightTo` and `NpcChaseNavigator.HasClearLine` both use a
body-width `SphereCast` (capsule radius) instead of a thin `Raycast`, so a corner that would clip the
body is treated as blocked.

### 5. Pathfinding timed out while chasing

**Symptom:** A* over the whole map issued a `Physics.CheckBox` per node per repath, exceeding the
frame budget during a chase.

**Fix:** `NpcPathfinder3D` caches walkability per search, and chase pathfinders use tighter bounds
(`searchPadding` 8, `maxNodes` 600).

## Tuning (`NpcDashMeleeConfig`)

| Field | Default | Meaning |
|---|---|---|
| `WarningDuration` | 0.5 | Telegraph length before the dash commits. |
| `WarningR/G/B/A` | 1 / 0.92 / 0.016 / 1 | Body tint during the warning (yellow). |
| `ApproachSpeed` | 2.8 | Movement while closing. |
| `DashRange` | 6 | Max distance at which a dash can start. |
| `DashSpeed` | 14 | Dash velocity. |
| `StoppingDistance` | 1.1 | Dash stops this far short of the player, then swings. |
| `RecoveryDuration` | 1.1 | Pause after the swing. |

See [Documents/NPC_MELEE_AI.md](NPC_MELEE_AI.md) for the ordinary proximity-melee enemy, and
[Documents/ISOMETRIC_3D.md](ISOMETRIC_3D.md) for the Town scene wiring.
