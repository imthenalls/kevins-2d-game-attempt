# Model/View Slice: NPC Health + Logical Cell

## Rule

> If we want to save it, do not store the authoritative value in a MonoBehaviour.

This first vertical slice migrates one NPC's saveable state out of Unity components. It is
deliberately narrow: one feature, one NPC, minimum changes elsewhere.

## Feature

The `sword_guard` NPC's **current HP** and **logical map cell** (grid coordinates).

| | Before | After |
|---|---|---|
| HP authority | `EntityStats._hp` (MonoBehaviour) | `Game.Core.NpcState.Hp` |
| Position authority | `Transform.position`, saved as world floats | `Game.Core.NpcState.CellX/CellY` (logical cell) |
| Save source | `SaveManager` read the MonoBehaviour | `SaveManager` reads the model via `NpcStateSnapshot` |

## Architecture

### Pure core — `Game.Data` (no UnityEngine)

`Assets/Scripts/GameData/Game.Data.asmdef` is an assembly with **`noEngineReferences: true`**, so
nothing in it can reference UnityEngine. It contains plain C# only:

| Type | Responsibility |
|---|---|
| `NpcState` | Authoritative HP, MaxHp, and logical cell; implements `IHealthModel`; raises `Changed`. |
| `NpcStateRepository` | Owns models, keyed by stable `npcId`, so they outlive any view. |
| `NpcStateService` | Command/service API: `ApplyDamage`, `Heal`, `MoveToCell`, `TryCapture`, `Apply`. |
| `NpcStateSnapshot` | Immutable save boundary value (no scene references). |
| `IHealthModel` | Interface a Unity facade delegates to. |
| `GameSession` | Scoped root that owns the repository + service. |

### Unity side — `Game.Presentation` (`Assets/Scripts/GamePresentation/`)

| Type | Responsibility |
|---|---|
| `GameSessionHost` | Composition root. Creates the `GameSession` (DontDestroyOnLoad); no large global static state. |
| `NpcStateView` | Adapter on the NPC. Registers the model by `npcId`, binds `EntityStats`, mirrors physics movement into the model as a command, and repositions the body from the model. |
| `EntityStats` | Optional facade: when bound to an `IHealthModel`, `Hp`/`MaxHp` read from the model and `TakeDamage`/`Heal`/`SetHp`/`IncreaseMaxHp`/`DecreaseMaxHp` delegate to it. Unbound entities behave exactly as before. |
| `SaveManager` / `SaveData` | Convert `NpcState` ↔ `NpcSaveEntry` (`hasModelState`, `cellX`, `cellY`, `hp`, `maxHp`). |

## Data flow

```
                 commands                         change notification
combat ──► EntityStats.TakeDamage ──► NpcState.ApplyDamage ──► NpcState.Changed
                                                                   │
                                                          NpcStateView.HandleModelChanged
                                                                   │
                                             stats.SetHpFromModel + reposition body to cell

physics/wander ──► Transform moves ──► NpcStateView.LateUpdate ──► NpcState.MoveToCell (command)
```

- The view never owns HP; `EntityStats.Hp` returns `model.Hp` while bound.
- Gameplay changes go through the model (via `EntityStats` delegation or `NpcStateService`).
- The model notifies the view, which refreshes presentation.

## Save / load

- **Save:** for an NPC with `NpcStateView`, `SaveManager` calls
  `NpcStateService.TryCapture(npcId, out NpcStateSnapshot)` and writes the ordinary values
  `hasModelState`, `hp`, `maxHp`, `cellX`, `cellY` into `NpcSaveEntry`. No scene references.
- **Load:** for an entry with `hasModelState`, `SaveManager` calls
  `NpcStateService.Apply(snapshot)`; the bound `NpcStateView` observes the change and refreshes
  the transform and `EntityStats`.
- NPCs without an adapter keep the legacy world-position path, so behavior is unchanged for them.

## Tests

`Assets/Tests/EditMode/` (recreated test assembly) — 8 tests, all passing, no Play Mode:

- damage/heal clamps and death
- logical cell move
- service commands change the model
- snapshot save/load round trip
- **view destroy + recreate keeps the model** (state not reset)
- the model compiles/runs without UnityEngine

Run the model anywhere — no Unity Editor and no scene required:

| Command | Needs |
|---|---|
| `dotnet test Tools/ModelHarness.Tests/ModelHarness.Tests.csproj` | .NET SDK (NUnit) |
| `dotnet run --project Tools/ModelHarness.Net/ModelHarness.Net.csproj` | .NET SDK (console harness) |
| `powershell -ExecutionPolicy Bypass -File Tools/ModelHarness/build-and-run.ps1` | only Unity's bundled Roslyn (no SDK) |
| `unity command run_tests --mode EditMode` | Unity Editor, no scene |

The `Tools/` projects link the `Assets/Scripts/GamePresentation/GameData` sources directly, so there is one copy of
the model and one copy of the NUnit test file.

Runtime verification (`editor_play` + eval): `hpStart=30/30 modelStart=30`;
`afterDamage stats=23 model=23`; `afterViewRecycle stats=23 model=23 cell=24,-17`.

## Transitional compromises

1. **Continuous world position is still the physics transform.** The logical **cell** is the
   saved authority; `NpcStateView.LateUpdate` writes cell changes into the model via
   `MoveToCell`. A future movement service could own this directly.
2. **`EntityStats` still exists on all entities.** It is only a facade for the bound NPC; other
   entities (player, other enemies) keep local HP. Converting them is out of scope for this slice.
3. **The NPC group `sword_guard` has two scene instances with the same `NpcController.NpcId`.**
   Only the first is adapted, to avoid two objects sharing one model. Duplicate `npcId`s are a
   pre-existing issue in `SaveManager` too.

## Remaining scene-owned state related to this feature

- The adapted NPC's authored `Transform.position` is still the scene spawn position (the model
  seeds its cell from it on first bind).
- The other `sword_guard` and all other NPCs still store HP/position in MonoBehaviours.
- `EntityStats` still keeps `_hp` as a presentation cache for unbound entities.

## Recommended next slice

**Migrate the player's HP/MP the same way** using the same `GameSession`/`IHealthModel` seam, or
**give NPCs stable unique ids** so the repository/save keys are unambiguous. The id fix is small
and removes the duplicate-`npcId` compromise before more data is moved onto the model.
