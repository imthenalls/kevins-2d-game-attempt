# Architecture Audit: Data / Presentation Split

> Status: point-in-time audit. The boundary mechanism is sound; the migration of
> authoritative state into `Game.Data` is only partially complete. This document records both,
> so the remaining work is explicit.

## Verdict

`Game.Data` cannot reference Unity or `Game.Presentation` — that is enforced by the compiler, not
by convention. Most authoritative state — NPC state, inventory contents, tuning, config, mana, world
facts, quests, equipment, hotbar, keys, player health, and the save shape — lives in the Core, and
the player's continuous physics position is a documented transitional compromise.

**However, the migration is not complete.** A re-audit of the NPC folder found one remaining
Shell-owned rule: residual `NpcProximityMelee3D` engagement decisions. It is tracked in **Deviations**
below and must not be described as migrated. (`NpcMemory`, the `NpcSchedule3D` state machine,
`NpcInventoryDatabase` starting-inventory seeding, and `NpcPerception` target ranking were migrated —
see **Migrated since the first audit**.)

This is also the project's named architecture: **Engine-Free Core**.

## How the boundary is enforced

| Mechanism | Evidence |
|---|---|
| Assembly isolation | `Assets/Scripts/GameData/Game.Data.asmdef` — `noEngineReferences: true`, empty `references` |
| No Unity types in data | 37 types in `GameData/`; no `MonoBehaviour` / `ScriptableObject`; engine identifiers appear only in doc comments |
| Engine-free build | `Tools/ModelHarness.Tests` compiles and runs `GameData` under plain .NET (no Unity) — 62 tests |
| One-way dependency | `Game.Presentation` and `Game.Presentation.Editor` reference `Game.Data`; nothing references back |
| Runtime checks | `Tools/verify-all.ps1` (compile, Edit Mode + Play Mode tests, `dotnet test`, scene smoke) |

## Where things belong

- **`Game.Data`** — pure C#: models, value types, interfaces, services, config, save DTOs, and
  validation. No UnityEngine.
- **`Game.Presentation`** — MonoBehaviours, UI, adapters, controllers, views, and content
  definitions (`ScriptableObject`). Owns wiring, references, and rendering — not authoritative state.
- **Namespace** — data models keep `Game.Core` even though the assembly is `Game.Data` (rule 4).

## Correctly separated today

| State | Data owner (`Game.Data`) | Unity side (`Game.Presentation`) |
|---|---|---|
| NPC HP / logical cell | `NpcState`, `NpcStateRepository`, `NpcStateService`, `NpcStateSnapshot` | `NpcStateView` binds the model to `EntityStats` + transform |
| Inventory contents | `InventoryModel`, `InventorySlot`, `IItem`, `ItemType` / `ItemFlags` / `ItemScope` | `ItemData : ScriptableObject, IItem` keeps icon / equip / use effects |
| Tuning values | `PlayerMovementConfig`, `EntityStatsConfig`, `CombatAttackerConfig`, `Npc*Config`, `SlidingDoorConfig`, `Portal*Config`, ... | components hold a `[SerializeField]` config and read it |
| Session root | `GameSession` | `GameSessionHost` (composition root, `DontDestroyOnLoad`) |
| Health contract | `IHealthModel` | `EntityStats` facade (delegates while bound) |
| Data validation | `IdIntegrity`, `ValidationIssue` | `GameDataValidator` (Editor) |
| Mana balance / capacity | `ManaAccount`, `WalletSaveData`, `WalletTransaction` | `Wallet` (MonoBehaviour facade) |
| World facts | `WorldFacts` | `WorldStateManager` (MonoBehaviour facade) |
| Quest runtime | `QuestGraphData`, `QuestInstance`, `ICondition`, `IQuestAction` | `QuestLoader` (factories via `QuestRuntimeBindings`), `QuestManager` (events + save) |
| Equipment / hotbar | `EquipmentModel`, `HotbarModel`, `EquipSlotType` | `EquipmentManager`, `HotbarUI` (cast `IItem`→`ItemData` for asset data) |
| Save shape | `SaveData`, `NpcSaveEntry`, `QuestSaveEntry`, `MarketTransaction`, `WalletSaveData` | `SaveManager` (JsonUtility read/write) |
| Key ownership | `Keyring` | `PlayerKeyring` (MonoBehaviour facade, `IKeyHolder` events) |
| Player health | `HealthModel` (owned by `GameSession`) | `PlayerController2D` binds it to `EntityStats`; `WorldTravelState` shares mana/positions only |
| Player position | `PositionModel` (owned by `GameSession`) | `PlayerController2D` mirrors the physics transform to/from it |
| Per-world remembered positions | `WorldPositionSaveEntry` (cell + local offset, v7) | `WorldTravelState` converts through the scene Grid |

## Migrated since the first audit

- **Mana** — logic moved to `Game.Core.ManaAccount`; `Wallet` is now a facade forwarding to it and
  re-raising its events. Engine-free tests: `Assets/Tests/EditMode/ManaAccountTests.cs`.
- **World facts** — logic moved to `Game.Core.WorldFacts`; `WorldStateManager` is now a facade
  (singleton + static event). Engine-free tests: `Assets/Tests/EditMode/WorldFactsTests.cs`.
- **Quests** — `QuestGraphData`, `QuestInstance`, and the `ICondition` / `IQuestAction` interfaces
  moved to `Game.Data`; `QuestLoader` stays in the Shell and wires factories through
  `QuestRuntimeBindings`. `QuestProgressionTests` moved to `Assets/Tests/EditMode`.
- **Equipment / hotbar** — `EquipmentModel`, `HotbarModel`, and `EquipSlotType` moved to `Game.Data`,
  keyed on `IItem` (with `IItem.EquipSlot`); `EquipmentManager` / `HotbarUI` stay as adapters.
  Engine-free tests: `Assets/Tests/EditMode/EquipmentModelTests.cs`.
- **Save shape** — `SaveData`, `NpcSaveEntry`, `QuestSaveEntry`, `MarketTransaction`, and
  `WalletSaveData` moved to `Game.Data`; `SaveManager` stays as the JsonUtility reader/writer.
  Engine-free tests: `Assets/Tests/EditMode/SaveDataShapeTests.cs`.
- **Key ownership** — logic moved to `Game.Core.Keyring`; `PlayerKeyring` is now a facade
  (singleton + `IKeyHolder`/`OnChanged`). Engine-free tests: `Assets/Tests/EditMode/KeyringTests.cs`.
- **Player health** — a `Game.Core.HealthModel` is owned by `GameSession` and bound to the player's
  `EntityStats` in `PlayerController2D.Awake`. The duplicate `WorldTravelState.sharedHp/sharedMaxHp`
  owner was deleted, so HP is now shared across avatars/scenes by the model instead of by copying.
  Engine-free tests: `Assets/Tests/EditMode/HealthModelTests.cs`.
- **Player position** — a `Game.Core.PositionModel` (logical grid cell + local offset) is owned by
  `GameSession`; `PlayerController2D` mirrors the physics transform to/from it and repositions the
  body when the model changes (load/teleport/avatar switch). `SaveData` stores the cell + offset
  (v7) and converts older float-only saves through the scene Grid on load. Engine-free tests:
  `Assets/Tests/EditMode/PositionModelTests.cs`.
- **Per-world remembered positions** — `WorldTravelState` now stores each world's return position as
  a grid cell + local offset (preferring the player's `PositionModel` when remembering the player),
  saves it in `WorldPositionSaveEntry` (v7), and converts legacy float-only entries through the
  scene Grid on load. Play Mode test: `World_Remembered_Position_RoundTrips_As_Cell`.
- **NPC memory** — persistent locked-gate knowledge and its remember/skip/forget rules moved to
  `Game.Core.NpcMemoryModel`, owned by `GameSession` (`NpcMemoryRepo`/`NpcMemories`) and saved via
  `NpcSaveEntry.lockedGates` (v9); `NpcMemory` is now a facade over stable gate ids. This also fixes
  the `Clear()` bug where persisted facts survived a local clear. Engine-free tests:
  `Assets/Tests/EditMode/NpcMemoryModelTests.cs`.
- **NPC home-schedule state machine** — the Away→ToHome→Home transitions, portal success/failure
  handling, retry timing, and the randomized initial Away leg moved into `Game.Core.NpcScheduleState`
  (events in / commands out: `NpcScheduleEvent` / `NpcScheduleCommand`). All schedule tuning
  (entry radius, waypoint threshold, retry, initial fraction) is in `NpcScheduleConfig` and the
  local-avoidance tuning is in `NpcLocalAvoidanceConfig`; `NpcSchedule3D` is now a facade that only
  performs the returned Unity operations. Engine-free tests:
  `Assets/Tests/EditMode/NpcScheduleStateTests.cs`.
- **NPC starting-inventory seeding** — parsed definitions are plain `Game.Core.NpcStartingInventory`
  / `NpcStartingItem` DTOs, and the seed-once decision plus the "already initialized" state moved into
  `Game.Core.NpcInventoryInitializationService` (owned by `GameSession`). The old "has ≥1 item" test
  was replaced by `InventoryModel.IsInitialized`, so a legitimately emptied inventory is not reseeded
  on a scene reload; the flag is saved via `NpcSaveEntry.inventoryInitialized` (v10). `NpcInventoryDatabase`
  is now a facade that only parses JSON, resolves `ItemData`, and applies the seed. Engine-free tests:
  `Assets/Tests/EditMode/NpcInventoryInitializationTests.cs`.
- **NPC perception tuning + target ranking** — the scan radius moved into `Game.Core.NpcPerceptionConfig`
  and the "nearest eligible target/gate within range" rule (open gates and invalid components filtered)
  moved into `Game.Core.NpcTargetSelection` (+ `NpcTargetCandidate`). `NpcPerception` now only samples
  `Physics2D`, resolves components, and discovers the player through `PlayerControllerBase` instead of
  `PlayerController2D`, so the same component works in 2D and 3D. Engine-free tests:
  `Assets/Tests/EditMode/NpcTargetSelectionTests.cs`.

## Deviations — authoritative state / rules still in Presentation

| # | State / rule | Location | Impact | Note |
|---|---|---|---|---|
| 1 | Residual engagement decisions | `NPCs/NpcProximityMelee3D.cs` | Uses `MeleeEngagementPolicy`, but tuning fields and target-validity/behavior-blocked transitions remain in the MonoBehaviour. | Extend the Core policy to take alive/behavior inputs and return an intent. |
| — | Player continuous physics position | `PlayerController2D/3D` | Grid-anchored via `PositionModel`; the raw physics transform is still Shell-owned. | Documented transitional compromise. |

## Practical consequence

The migration is **in progress**, not complete. Most authoritative state lives in the Engine-Free
Core with fast engine-free tests, but the one deviation above still places gameplay rules in
`Game.Presentation`.

## Recommended migration order

Work top-down; each step must keep `Tools/verify-all.ps1` green and add engine-free tests.

1. **`NpcProximityMelee3D`** → extend `MeleeEngagementPolicy` to return an intent (Idle / Blocked /
   Chase / Attack / Disengage) from tuning + alive/behavior inputs.

Each step should keep `Tools/verify-all.ps1` green and move the affected tests into
`Assets/Tests/EditMode` where they can then run under `dotnet test`.

## Re-verifying this audit

```
rg -n "UnityEngine|MonoBehaviour|ScriptableObject|Vector2|Vector3|Mathf|GameObject|Transform" Assets/Scripts/GameData --glob "*.cs"
powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1
```

The first command should keep returning **only comment matches**.
