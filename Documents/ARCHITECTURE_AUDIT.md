# Architecture Audit: Data / Presentation Split

> Status: point-in-time audit. The boundary mechanism is sound; the migration of
> authoritative state into `Game.Data` is only partially complete. This document records both,
> so the remaining work is explicit.

## Verdict

`Game.Data` cannot reference Unity or `Game.Presentation` — that is enforced by the compiler, not
by convention. Authoritative state — NPC state, inventory, tuning, config, mana, world facts,
quests, equipment, hotbar, keys, player health, and the save shape — now lives in the Core. The
only remaining Shell-owned authoritative value is the player's continuous physics position, which
is a documented transitional compromise. See [AGENT.md](../AGENT.md) → **Architecture: Engine-Free
Core** for the pattern and rules.

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

## Deviations — authoritative state still in Presentation

| # | State | Location | Impact | Note |
|---|---|---|---|---|
| 1 | Player world position | physics `Transform`, saved as floats | Documented transitional compromise | [MODEL_VIEW_SLICE.md](MODEL_VIEW_SLICE.md) compromise 1 |

## Practical consequence

The only remaining Shell-owned authoritative state is the player's continuous physics position,
which is an intentional compromise (a logical cell model could replace it if needed). Every other
migrated system is engine-free state with fast tests.

## Recommended migration order

The planned migrations are complete. The only open item is optional:

1. **Player physics position** (optional/structural) — introduce a logical position model if
   continuous world position ever needs to be authoritative.

Each step should keep `Tools/verify-all.ps1` green and move the affected tests into
`Assets/Tests/EditMode` where they can then run under `dotnet test`.

## Re-verifying this audit

```
rg -n "UnityEngine|MonoBehaviour|ScriptableObject|Vector2|Vector3|Mathf|GameObject|Transform" Assets/Scripts/GameData --glob "*.cs"
powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1
```

The first command should keep returning **only comment matches**.
