# Architecture Audit: Data / Presentation Split

> Status: point-in-time audit. The boundary mechanism is sound; the migration of
> authoritative state into `Game.Data` is only partially complete. This document records both,
> so the remaining work is explicit.

## Verdict

`Game.Data` cannot reference Unity or `Game.Presentation` — that is enforced by the compiler, not
by convention. State that has been migrated (NPC state, inventory, tuning, validation, session,
mana, world facts) is correctly separated. A handful of systems still keep **authoritative,
saveable state inside `Game.Presentation`**, which is a deviation from AGENT.md rule 1
("if we want to save it, do not store the authoritative value in a MonoBehaviour"). See
[AGENT.md](../AGENT.md) → **Architecture: Engine-Free Core** for the pattern and rules.

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

## Migrated since the first audit

- **Mana** — logic moved to `Game.Core.ManaAccount`; `Wallet` is now a facade forwarding to it and
  re-raising its events. Engine-free tests: `Assets/Tests/EditMode/ManaAccountTests.cs`.
- **World facts** — logic moved to `Game.Core.WorldFacts`; `WorldStateManager` is now a facade
  (singleton + static event). Engine-free tests: `Assets/Tests/EditMode/WorldFactsTests.cs`.

## Deviations — authoritative state still in Presentation

| # | State | Location | Impact | Note |
|---|---|---|---|---|
| 1 | Player HP / MP / Max | `GamePresentation/Entity/EntityStats.cs:30-31` | Player stats are not model-backed | NPCs are fine (bound via `NpcStateView`); the player is the documented next slice in [MODEL_VIEW_SLICE.md](MODEL_VIEW_SLICE.md) |
| 2 | Quest runtime (active nodes, objective counts) | `GamePresentation/Quests/QuestInstance.cs:22-23` | Quest logic uses `Mathf` (line 188) and cannot be tested engine-free | `EquipmentModel` and `HotbarModel` have the same shape |
| 3 | Key ownership | `GamePresentation/Inventory/PlayerKeyring.cs:19` | Saveable key state lives in a MonoBehaviour | May be forced by Unity prefab/tag needs — verify before moving |
| 4 | Save DTO | `GamePresentation/GameManagement/SaveData.cs:29` | The save shape references presentation types (`QuestManager.QuestSaveEntry`, `MarketTransaction`) | Rule 1 says save DTOs belong in `Game.Data` |
| 5 | Player world position | physics `Transform`, saved as floats | Documented transitional compromise | [MODEL_VIEW_SLICE.md](MODEL_VIEW_SLICE.md) compromise 1 |

## Practical consequence

Deviations 1 and 2 are why `HotbarModelTests`, `EquipmentIntegrationPlayModeTests`, and
`QuestProgressionTests` live in `Assets/Tests/Presentation` (Unity-only) instead of the fast
`Assets/Tests/EditMode` bucket that `dotnet test` mirrors. Moving those types into `Game.Data` is
what buys engine-free tests for quests, equipment, and the hotbar.

## Recommended migration order

1. **`QuestInstance` / `EquipmentModel` / `HotbarModel` → `Game.Data`** (replace `Mathf`).
   Unlocks engine-free tests for quests, equipment, and hotbar.
2. **`SaveData` DTO → `Game.Data`** — split quest and wallet DTOs out so the data layer owns the
   save shape and no longer depends on presentation types.
3. **`PlayerKeyring`** behind a data model (mind prefab/tag needs).
4. **Player HP/MP** through the existing `IHealthModel` seam (the `MODEL_VIEW_SLICE.md` follow-up).

Each step should keep `Tools/verify-all.ps1` green and move the affected tests into
`Assets/Tests/EditMode` where they can then run under `dotnet test`.

## Re-verifying this audit

```
rg -n "UnityEngine|MonoBehaviour|ScriptableObject|Vector2|Vector3|Mathf|GameObject|Transform" Assets/Scripts/GameData --glob "*.cs"
powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1
```

The first command should keep returning **only comment matches**.
