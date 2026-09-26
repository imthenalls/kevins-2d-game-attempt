# Architecture Guardrails (Engine-Free Core)

**Purpose:** stop gameplay logic and authoritative state from quietly accumulating in
`GamePresentation`. This file is the checklist to run before merging *any* new MonoBehaviour.

## The rule (restated)

`Game.Data` (namespace `Game.Core`) owns **all** of:

- authoritative, saveable **state**,
- **rules and decisions** (AI choices, phase machines, cooldowns, engagement, lock/trade/loot
  policy, spawn scheduling),
- tuning (`*Config`),
- pathfinding/algorithm logic.

`Game.Presentation` **displays and drives** Core. A MonoBehaviour may hold Unity-only references
(`Transform`, `Sprite`, `Collider`, `LayerMask`) and presentation cache — never a gameplay rule or
authoritative value.

> "The rule: Core owns state **and decides**; the Shell displays and drives it."

## The thin-facade pattern

When a MonoBehaviour must exist (Inspector fields, prefab identity, physics/lifecycle), keep it a
**facade**: it owns serialized fields + Unity I/O and forwards every decision to a plain-C# Core
type. Existing correct examples:

| Shell (MonoBehaviour) | Core (plain C#) |
|---|---|
| `Wallet` | `ManaAccount` |
| `WorldStateManager` | `WorldFacts` |
| `NpcStateView` | `NpcState` |
| `NpcSchedule3D` | `NpcScheduleState` |
| `NpcSchedule3D` / `NpcWander3D` (movement recovery) | `TravelRecoveryModel` |
| `PlayerController2D/3D` (position) | `PositionModel` |
| `EntityStats` (when bound) | `HealthModel` / `NpcState` |

## Mandatory pre-merge checklist

Run this for every new or edited MonoBehaviour:

1. **Does it hold a gameplay rule, decision, state machine, or timer/cooldown?**
   (e.g. "if the player is within X, chase"; "pick a random destination"; phase transitions;
   hit selection; whether a key is required.) → Extract a plain-C# model in `Game.Core`.
2. **Does it hold a value that would be saved or should outlive the scene/view?** → Move it into a
   `Game.Core` model owned by `GameSession` (or another Core service).
3. **Is the same logic implemented a second time (2D + 3D, player + NPC)?** → The shared rules go
   in Core once; each dimension is a thin adapter. Duplicated logic is a bug.
4. **Are enums that describe gameplay (`NpcBehaviorState`, `NpcType`, phases) declared in
   Presentation?** → They belong in `Game.Core`.
5. **Is there an authority duplicated in the Shell** (a second HP store, a second key store, a
   second position store)? → Delete it; wrap the Core type.
6. **Is `Game.Data` free of `UnityEngine`?** The assembly enforces it (`noEngineReferences: true`).
7. **Can the rule be unit-tested in `Assets/Tests/EditMode` with no scene?** If not, the split is
   probably wrong.

If a MonoBehaviour "drives" but also "decides", it fails the review.

## Debt tracker (audit of `GamePresentation`)

Migrate top-down. Update this table as items land.

### Tier 1 — NPC movement / AI decisions
| Status | Where | Decision to extract into Core |
|---|---|---|
| ✅ | `NPCs/NpcPathfinder3D.cs`, `NpcPathfinder.cs` | A* algorithm → `Game.Core.GridPathfinder` over `IWalkabilityGrid` (both facades done) |
| ✅ | `NPCs/NpcProximityMelee3D.cs`, `NpcProximityMeleeController.cs` | Engage/chase/attack/disengage → `Game.Core.MeleeEngagementPolicy` (both facades done) |
| ✅ | `NPCs/NpcDashMelee3D.cs`, `NpcDashMeleeController.cs` | Approach/Warning/Dash/Swing/Recovery → `Game.Core.NpcDashMeleeModel` (both facades done) |
| ✅ | `NPCs/NpcWander3D.cs`, `NpcWanderBehavior.cs` | Wander target/idle/stall/arrival + dead-end memory → `Game.Core.WanderModel` (both facades done; stall/repath recovery shared via `TravelRecoveryModel`) |
| ✅ | `NPCs/NpcSchedule3D.cs` | Home-trip stall/repath/abandon policy → `Game.Core.TravelRecoveryModel` (facade applies movement, pathfinding and portal travel) |
| ✅ | `NPCs/NpcBehaviorManager.cs` | Weighted behavior selection → `Game.Core.NpcBehaviorScheduler` |
| ✅ | `NPCs/NpcIdleBehavior.cs` | Idle-timer rule → `Game.Core.IdleTimer` |

### Tier 2 — Combat decisions
| Status | Where | Decision to extract |
|---|---|---|
| ✅ | `Entity/CombatAttacker.cs` | Cooldown, input buffer, once-per-swing hit registry, recoil → `Game.Core.AttackModel` |
| ✅ | `Entity/CombatReceiver.cs` | Damage gate + scaling + enemy-death rule → `Game.Core.DamagePolicy` (`CanReceive`, `Resolve`, `DropsLootOnDeath`); the facade applies the side effects (loot/hide/event). |
| ✅ | `Inventory/Equipment/EquippedWeaponVisual3D.cs` | Reach/cone hit selection → `Game.Core.WeaponSwingPolicy`; the facade keeps the 3D overlap query. |

### Tier 3 — State on MonoBehaviours
| Status | Where | State to move |
|---|---|---|
| 🟨 | `NPCs/NpcController.cs` | `NpcBehaviorState` / `NpcType` enums moved to `Game.Core` ✅ (shared by all adapters, not Presentation). Transient `behaviorState`, movement-lock memory and `AggroRange` stay on the MonoBehaviour (not saveable). |
| ✅ | `NPCs/NpcKeyring.cs` | Facade over `Game.Core.Keyring` (raw `AddKey(id)` seed overload added to Core) |
| ✅ | `Entity/EntityStats.cs` | HP always delegates to an `IHealthModel` (private fallback `Game.Core.HealthModel`), MP to a `Wallet`/`ManaAccount` or a fallback `ManaAccount`, bonuses to `Game.Core.StatBonuses`. The MonoBehaviour holds no authoritative HP/MP. |
| ✅ | `GameManagement/SceneRulesManager.cs` | The active rule set is data applied by the adapter (the ScriptableObject is the serialization boundary); no Core model is warranted without migrating the asset. |

### Tier 4 — Rule containers
| Status | Where | Rules to move |
|---|---|---|
| 🟨 | `GameManagement/SceneRules.cs` + `SceneRulesManager` | Player-death rule + `PlayerDeathBehavior` enum → `Game.Core.PlayerDeathPolicy` ✅. The ScriptableObject stays as the serialization adapter; the multiplier/DOT/HOT fields are data it applies. |
| 🟨 | `World/SlidingDoor.cs` | Lock rule → `Game.Core.DoorLockPolicy` ✅. Key resolution, animation and auto-close remain the Unity adapter. |
| ✅ | `Economy/TradeService.cs` | Moved to `Game.Core.TradeService` (+ `TradeRequest`/`TradeResult`/`TradeFailure`/`ITradeParticipant`). Operates on `ManaAccount`/`InventoryModel`; the Shell bridges `OnTradeCompleted` to `QuestEventBus` via `TradeQuestBridge`. |
| 🟨 | `Portals/PortalManager.cs` | Access rule → `Game.Core.PortalAccessPolicy` ✅; cooldown and scene lookup remain the adapter. |
| ✅ | `World/EnemyLootDrop.cs` | Randomized loot quantity → `Game.Core.LootTable` |
| ✅ | `World/LootContainer.cs` | The looted flag is an adapter-side naming convention; no rule to extract. |
| ✅ | `World/TrainingEnemySpawner.cs` | Spawn gating (one living enemy, clear area) is orchestration of Unity instantiation; no Core rule. |
| ✅ | `WorldState/WorldStateNpcReactor.cs`, `NPCs/NpcDialogue.cs` | Flag reactions are orchestration; the gift transfer already goes through `InventoryTransferService`. |

Legend: ⬜ todo, 🟨 in progress, ✅ done.

**Landed so far (Core types in `Assets/Scripts/GameData/`):** `GridPathfinder` + `IWalkabilityGrid`,
`MeleeEngagementPolicy`, `NpcDashMeleeModel` (+ `NpcDashPhase`/`NpcDashIntent`/`NpcDashDecision`),
`AttackModel`, `WanderModel`, `TravelRecoveryModel`, `NpcBehaviorScheduler`, `IdleTimer`,
`DamagePolicy`. The 2D and 3D pathfinder/melee/dash/attack/wander/schedule components and the
behavior manager/idle/receiver are now thin facades over them. Engine-free tests:
`Assets/Tests/EditMode/{GridPathfinderTests,MeleeEngagementPolicyTests,NpcDashMeleeModelTests,AttackModelTests,WanderModelTests,TravelRecoveryModelTests,NpcBehaviorSchedulerTests,IdleTimerTests,DamagePolicyTests}.cs`.

Also in Core: `NpcBehaviorState` / `NpcType` enums, `TradeService` (+ `TradeRequest`/`TradeResult`/`TradeFailure`/`ITradeParticipant`), the raw `Keyring.AddKey(id)` seed path, `StatBonuses`, `PlayerDeathBehavior`/`PlayerDeathPolicy`, `LootTable`, `DoorLockPolicy`, `PortalAccessPolicy`, and `WeaponSwingPolicy`. `NpcKeyring` is now a facade over `Game.Core.Keyring`.

**Remaining non-Core entries are orchestration or serialization adapters** — one-line conditions and Unity instantiation/animation, which the guardrail intentionally leaves in the Shell. No authoritative gameplay state remains on a MonoBehaviour.

## Known-good (do not "fix")

`Wallet`, `WorldStateManager`, `QuestManager`, `SceneLoader`, `GameBootstrap`, `NpcSchedule3D`
(phase/timer), `PlayerController*` (position only), and all `*Config` tuning. Pure rendering,
input reading, sprite/billboard, physics application, and editor tooling are correctly in the Shell.

## Definition of done for a migration item

1. The rule lives in a plain-C# `Game.Core` type with no `UnityEngine`.
2. The MonoBehaviour is a facade that only does Unity I/O and forwards decisions.
3. The old copy is deleted (no parallel implementation).
4. An EditMode unit test exercises the Core rule with no scene.
5. `Tools/verify-all.ps1` passes (compile, tests, smoke).
