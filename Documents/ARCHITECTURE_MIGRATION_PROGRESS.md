# Architecture Migration Progress (handoff)

> Living handoff for the **Engine-Free Core** migration of the NPC systems. Read this first when
> resuming the work in a new session, then read
> [ARCHITECTURE_GUARDRAILS.md](ARCHITECTURE_GUARDRAILS.md) (the rule + per-file debt table) and
> [ARCHITECTURE_AUDIT.md](ARCHITECTURE_AUDIT.md) (the current audit and deviations).

## The rule in one line

`Game.Data` (namespace `Game.Core`) owns all state **and decisions** (AI choices, phase machines,
tuning config, algorithms). `Game.Presentation` MonoBehaviours are thin facades that do Unity I/O and
forward every decision to Core. No `UnityEngine` type may enter `Game.Data`.

## How to verify (run after every step)

```
powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1
```

It runs: Unity compile → Edit Mode tests → Play Mode tests → `dotnet test` → scene smoke. Green means
safe to commit. Engine-free tests live in `Assets/Tests/EditMode/` and are mirrored by
`dotnet test Tools/ModelHarness.Tests`.

## Done (committed)

| Migration | Commit | What moved to Core |
|---|---|---|
| NPC route following + local-avoidance math | `faf74a7` | `Game.Core.RouteFollower` (+`PathPoint`), `Game.Core.LocalAvoidance` (+`NeighborSample`). Fixed the wanderer freeze bug (missing `pathIndex` advance); ported all 5 followers (2D + 3D). Tests: `RouteFollowerTests`, `LocalAvoidanceTests`. |
| NPC memory | `1854e76` | `Game.Core.NpcMemoryModel` + repo/service/snapshot, owned by `GameSession`, saved via `NpcSaveEntry.lockedGates` (v9). Fixed the `Clear()` bug. Tests: `NpcMemoryModelTests`. |
| NPC home-schedule state machine | `5bc58d1` | `Game.Core.NpcScheduleState` is now a state machine: events in (`NpcScheduleEvent`) / commands out (`NpcScheduleCommand`). All schedule tuning in `NpcScheduleConfig`; local-avoidance tuning in `NpcLocalAvoidanceConfig`. `NpcSchedule3D` is a facade. Tests: `NpcScheduleStateTests`. |
| NPC starting-inventory seeding | `9400249` | Parsed definitions → `Game.Core.NpcStartingInventory`/`NpcStartingItem`; seed-once state → `Game.Core.NpcInventoryInitializationModel`/`NpcInventoryInitializationService` owned by `GameSession` (saved via `NpcSaveEntry.inventoryInitialized`, v10). `InventoryModel.IsInitialized` replaces the old "has ≥1 item" check, so an emptied inventory is not reseeded. `NpcInventoryDatabase` is a facade. Tests: `NpcInventoryInitializationTests`. |

## Remaining migrations (in order)

Follow the same pattern: add Core type(s) + a config, make the MonoBehaviour a facade, add engine-free
tests, run `verify-all`, update the two docs above, commit.

### 1. `NpcPerception` — tuning + target ranking → Core

- **Violation:** `Assets/Scripts/GamePresentation/NPCs/NpcPerception.cs`. `scanRadius` is a
  MonoBehaviour field; "nearest target" and "nearest gate (open doors are invalid)" are decided in the
  component; player discovery is hardwired to `PlayerController2D` (breaks 3D Town).
- **Target:** add `Game.Core.NpcPerceptionConfig` (scan radius etc.). Keep the `Physics`/`Physics2D`
  overlap sampling + component resolution in the Shell; move target/gate ranking into an engine-free
  policy operating on candidate distance/state data. Use `PlayerControllerBase` (or 2D/3D adapters) so
  it works in both dimensions.
- **Tests:** ranking policy (nearest wins, open doors filtered, dead/irrelevant filtered).

### 2. `NpcProximityMelee3D` — remaining engagement decisions → Core

- **Violation:** `Assets/Scripts/GamePresentation/NPCs/NpcProximityMelee3D.cs` (and its 2D sibling
  `NpcProximityMeleeController.cs`). It uses `MeleeEngagementPolicy`, but `chaseSpeed`,
  `disengageRangeMultiplier`, `repathInterval` are raw serialized fields, and target validity /
  behavior-blocked transitions / combat enter-exit are decided in the MonoBehaviour.
- **Target:** add a Core `NpcMeleeEngagementConfig` (or extend the existing config). Expand
  `Game.Core.MeleeEngagementPolicy` to accept target-alive and behavior-state inputs and return an
  intent (`Idle` / `Blocked` / `Chase` / `Attack` / `Disengage`). Keep player lookup, path queries,
  velocity, and attack execution in the Shell. `IsEngaged` stays transient presentation state.
- **Tests:** intent table across range/alive/blocked combinations.

## Known open item (not part of this migration)

In a live Town probe, two NPCs stayed stationary: `Town NPC 4` (enabled wanderer, `Away`) and
`Town NPC 9` (`ToHome`). Likely their body overlaps wall geometry so `HitsWall` is true in every
direction and `NpcPathfinder3D` cannot find a walkable start cell (returns null → target acquisition
fails → permanent idle). This is a spawn/geometry issue, not the follower. Investigate separately.

## Useful commands

| Command | Purpose |
|---|---|
| `powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1` | Full verification suite |
| `unity command recompile --non-interactive` then `unity command recompile_status --non-interactive` | Compile + read errors |
| `unity command run_tests --mode EditMode --non-interactive` | Run Edit Mode tests |
| `dotnet test Tools/ModelHarness.Tests -c Release` | Fast engine-free mirror |

## Working agreement

- Keep each migration in its own commit; keep `verify-all` green before committing.
- Update `ARCHITECTURE_GUARDRAILS.md` (per-file row) and `ARCHITECTURE_AUDIT.md` (deviations + order)
  in the same commit as the code.
- Do not edit `*.unity` / `*.prefab` unless the request is specifically about them.
- The user reviews migrations and flags further violations; re-audit the NPC folder as needed.
