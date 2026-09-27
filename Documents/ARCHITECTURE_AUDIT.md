# Architecture Audit: Data / Presentation Split

> Status: point-in-time audit. The boundary mechanism is sound; the migration of
> authoritative state into `Game.Data` is only partially complete. This document records both,
> so the remaining work is explicit.

## Verdict

`Game.Data` cannot reference Unity or `Game.Presentation` — that is enforced by the compiler, not
by convention. Most authoritative state — NPC state, inventory contents, tuning, config, mana, world
facts, quests, equipment, hotbar, keys, player health, and the save shape — lives in the Core, and
the player's continuous physics position is a documented transitional compromise.

A **second re-audit** widened the scope beyond the NPC folder and found seven further
Presentation-owned gameplay models (world travel/progression, player dash, pending quest rewards,
one-time world objects, NPC door/recovery/navigation, character statistics, and rule-trigger
policy). Those are now migrated too; see **Migrated since the second audit**. Every Shell-owned NPC
rule and persistent state found by the first re-audit is also in Core (`NpcMemory`, the
`NpcSchedule3D` state machine, `NpcInventoryDatabase` starting-inventory seeding, `NpcPerception`
target ranking, and the residual `NpcProximityMelee3D` engagement decisions). The only remaining
deviation is the documented transitional compromise for the player's continuous physics position.

This is also the project's named architecture: **Engine-Free Core**.

## How the boundary is enforced

| Mechanism | Evidence |
|---|---|
| Assembly isolation | `Assets/Scripts/GameData/Game.Data.asmdef` — `noEngineReferences: true`, empty `references` |
| No Unity types in data | 133 top-level types in `GameData/`; no `MonoBehaviour` / `ScriptableObject`; engine identifiers appear only in doc comments |
| Engine-free build | `Tools/ModelHarness.Tests` compiles and runs `GameData` under plain .NET (no Unity) — 245 tests |
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
| World travel / progression | `WorldTravelModel` (+ `WorldLayer`, `RememberedWorldPosition`), owned by `GameSession` | `WorldTravelState` (facade: scene load, Grid conversion, character activation, stats binding) |
| Player dash | `PlayerDashModel` (+ `PlayerDashCommand`) | `PlayerController2D` / `PlayerController3D` (input, Rigidbody motion, trail) |
| Pending quest rewards | `PendingRewardLedger` (+ `PendingRewardDelivery`, `PendingRewardClaimResult`), owned by `GameSession` | `PendingRewardManager` (ItemData lookup, inventory bridge) |
| One-time world objects | `WorldObjectInteractionModel` (completed set in `WorldFacts`) | `WorldObject` (dialogue paging, reward delivery, activation) |
| NPC door use | `NpcUseDoorModel` (+ `NpcDoorPhase` / `NpcDoorCommand` / `NpcDoorObservation`) | `NpcUseDoorBehavior` (gate lookup, pathfinding, memory/quest side effects) |
| Chase navigation | `NpcChaseNavigationPolicy` (+ `NpcChaseNavigationConfig` / `NpcChaseNavigationDecision`) | `NpcChaseNavigator` (raycast line-of-sight, pathfinding, motion) |
| Character statistics | `CharacterStatisticsModel` (+ `CharacterStatisticsRepository`, `CharacterStatisticsSnapshot`), owned by `GameSession` | `CharacterStatistics` (combat-event sampling, convenience events) |
| Rule triggers | `RuleTriggerPolicy` (+ `SceneRuleTarget`, `RuleTriggerFireOn`) | `RuleTrigger2D` (collider callbacks, tag filter, SceneRulesManager calls) |

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
  (events in / commands out: `NpcScheduleEvent` / `NpcScheduleCommand`). The behavior-state pause
  rule (a non-Idle NPC — talking/combat/disabled — suspends the timer and returns
  `NpcScheduleCommand.Pause`) is also Core-owned via `Tick(delta, config, behaviorState)`. All
  schedule tuning (entry radius, waypoint threshold, retry, initial fraction) is in
  `NpcScheduleConfig` and the local-avoidance tuning is in `NpcLocalAvoidanceConfig`; `NpcSchedule3D`
  is now a facade that only performs the returned Unity operations. Engine-free tests:
  `Assets/Tests/EditMode/NpcScheduleStateTests.cs`.
- **NPC starting-inventory seeding** — parsed definitions are plain `Game.Core.NpcStartingInventory`
  / `NpcStartingItem` DTOs, and the seed-once decision plus the "already initialized" state moved into
  `Game.Core.NpcInventoryInitializationService` (owned by `GameSession`). The old "has ≥1 item" test
  was replaced by `InventoryModel.IsInitialized`, so a legitimately emptied inventory is not reseeded
  on a scene reload; the flag is saved via `NpcSaveEntry.inventoryInitialized` (v10). `NpcInventoryDatabase`
  is now a facade that only parses JSON, resolves `ItemData`, and applies the seed. Engine-free tests:
  `Assets/Tests/EditMode/NpcInventoryInitializationTests.cs`.
- **NPC perception tuning + target ranking** — the scan radius moved into `Game.Core.NpcPerceptionConfig`,
  the "nearest eligible target/gate within range" rule into `Game.Core.NpcTargetSelection`
  (+ `NpcTargetCandidate`), and gate eligibility (a gate is a target only when it exists and is
  closed) into `NpcTargetSelection.IsGateEligible`. `NpcPerception` only samples `Physics2D`, resolves
  components, and discovers the player through `PlayerControllerBase`; note the sensor itself is
  still a 2D `Physics2D` scan (only player discovery is dimension-agnostic). Engine-free tests:
  `Assets/Tests/EditMode/NpcTargetSelectionTests.cs`.
- **NPC melee engagement decisions** — chase/disengage/repath tuning moved into
  `Game.Core.NpcMeleeEngagementConfig`, and `Game.Core.MeleeEngagementPolicy` now takes target-alive
  and behavior-state inputs and returns `Idle` / `Blocked` / `Disengage` / `Chase` / `Attack`.
  `NpcProximityMelee3D` and `NpcProximityMeleeController` are now facades: they sample `targetAlive`
  (present and living) and the behavior state, pass both to the policy, and apply the returned intent
  — including Core's `Idle` for an absent/dead target. Player lookup, pathing, velocity, and attack
  execution stay in the Shell. Engine-free tests:
  `Assets/Tests/EditMode/MeleeEngagementPolicyTests.cs`.

## Migrated since the second audit

- **World travel / progression** — `WorldLayer` moved to Core and a `Game.Core.WorldTravelModel`
  (active world, per-world remembered logical positions, ability unlocks, shared wallet snapshot) is
  owned by `GameSession.WorldTravel`. `WorldTravelState` is now a facade: scene loading, Grid
  conversion, character activation, and transform/stats binding stay in the Shell. `SaveData` shape
  is unchanged (`WorldPositionSaveEntry` / `WorldAbilitySaveEntry`). Engine-free tests:
  `Assets/Tests/EditMode/WorldTravelModelTests.cs`.
- **Player dash** — the duplicated 2D/3D dash charges, cooldown, recharge timing, duration, and
  eligibility moved to `Game.Core.PlayerDashModel` (+ `PlayerDashCommand`); both controllers feed it
  elapsed time and a duration and execute the returned commands, keeping input, Rigidbody motion,
  and trails in the Shell. Engine-free tests: `Assets/Tests/EditMode/PlayerDashModelTests.cs`.
- **Pending quest rewards** — merging, remaining quantities, claim results, and the save snapshot
  moved to `Game.Core.PendingRewardLedger` (owned by `GameSession.PendingRewards`); the
  `PendingRewardManager` only resolves `ItemData` and bridges to `InventoryHelper`. Engine-free
  tests: `Assets/Tests/EditMode/PendingRewardLedgerTests.cs`.
- **One-time world objects** — completion/one-time-use rules moved to
  `Game.Core.WorldObjectInteractionModel`, recording completion in `WorldFacts` under a stable
  per-object key so a reloaded scene or a restart cannot duplicate the reward. `WorldObject` keeps
  dialogue paging, reward delivery, and activation. Engine-free tests:
  `Assets/Tests/EditMode/WorldObjectInteractionModelTests.cs`.
- **NPC door / recovery / navigation** — the use-door phase machine moved to
  `Game.Core.NpcUseDoorModel` (phases + `NpcDoorCommand` / `NpcDoorObservation`); the
  `NpcBehaviorBase` stall timer was replaced with `Game.Core.TravelRecoveryModel`; and the chase
  repath cadence / direct-step fallback moved to `Game.Core.NpcChaseNavigationPolicy` (+
  `NpcChaseNavigationConfig` / `NpcChaseNavigationDecision`). Raycasts, pathfinding, Rigidbody
  changes, and door lookup stay in the Shell. Engine-free tests:
  `Assets/Tests/EditMode/NpcUseDoorModelTests.cs`,
  `Assets/Tests/EditMode/NpcChaseNavigationPolicyTests.cs`.
- **Character statistics** — cumulative counters moved to `Game.Core.CharacterStatisticsModel`,
  registered per character by `GameSession.Statistics`; `CharacterStatistics` forwards combat/economy
  events and the player's totals are saved via `SaveData.playerStatistics` (v11). Engine-free tests:
  `Assets/Tests/EditMode/CharacterStatisticsModelTests.cs`.
- **Rule triggers** — `SceneRuleTarget` / `RuleTriggerFireOn` moved to Core and the enter/exit
  inversion + one-shot rule to `Game.Core.RuleTriggerPolicy`; `RuleTrigger2D` keeps the collider
  callbacks, tag filter, and `SceneRulesManager` calls. One-shots can persist via stable trigger ids
  and `RuleTriggerPolicy.FiredKey` in `WorldFacts`. Engine-free tests:
  `Assets/Tests/EditMode/RuleTriggerPolicyTests.cs`.

## Deviations — authoritative state / rules still in Presentation

| # | State / rule | Location | Impact | Note |
|---|---|---|---|---|
| — | Player continuous physics position | `PlayerController2D/3D` | Grid-anchored via `PositionModel`; the raw physics transform is still Shell-owned. | Documented transitional compromise. |

## Practical consequence

Both tracked migration passes are complete: no authoritative gameplay rule or saveable state found
by either audit is left in `Game.Presentation`. The only remaining item is the deliberate position
compromise, which is grid-anchored by `PositionModel`.

## Recommended migration order

None outstanding. Re-audit the whole `GamePresentation` tree (not just the NPC folder) before
relying on this document, since the second pass found models outside the NPC folder.

Each step should keep `Tools/verify-all.ps1` green and move the affected tests into
`Assets/Tests/EditMode` where they can then run under `dotnet test`.

## Re-verifying this audit

```
rg -n "UnityEngine|MonoBehaviour|ScriptableObject|Vector2|Vector3|Mathf|GameObject|Transform" Assets/Scripts/GameData --glob "*.cs"
powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1
```

The first command should keep returning **only comment matches**.
