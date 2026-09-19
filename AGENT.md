# AGENT.md

## Reference Documents

Full documentation for each system lives in the `Documents/` folder. Read the relevant file before making changes to that system.

| Document | Contents |
|---|---|
| [Documents/UNITY_DEVELOPER_SKILL.md](Documents/UNITY_DEVELOPER_SKILL.md) | Unity 6 LTS development skill: architecture, performance, rendering, testing, and deployment guidance |
| [Documents/PLAYER.md](Documents/PLAYER.md) | Player components, movement, interaction, stats UI |
| [Documents/PLAYER_DASH.md](Documents/PLAYER_DASH.md) | Left Shift directional dash, distance/speed tuning, and movement-lock behavior |
| [Documents/STATS.md](Documents/STATS.md) | EntityStats HP/MP system, events, and methods |
| [Documents/ENTITY.md](Documents/ENTITY.md) | Entity folder: IEntityController, EntityStats, CombatReceiver, CombatAttacker |
| [Documents/COMBAT.md](Documents/COMBAT.md) | Combat system: DamageInfo, CombatReceiver, CombatAttacker, scene setup |
| [Documents/NPC.md](Documents/NPC.md) | NPC controller, behaviors, dialogue, enemy setup |
| [Documents/NPC_ITEM_GIFTS.md](Documents/NPC_ITEM_GIFTS.md) | Give an item to the player when an NPC conversation is completed |
| [Documents/INVENTORY.md](Documents/INVENTORY.md) | Inventory model, UI, item data, canvas setup |
| [Documents/PORTAL.md](Documents/PORTAL.md) | Portal trigger, manager, spawn points, JSON schema |
| [Documents/QUEST_SYSTEM.md](Documents/QUEST_SYSTEM.md) | Quest graph architecture, JSON schema, runtime flow |
| [Documents/FUNCTIONS.md](Documents/FUNCTIONS.md) | Per-script function reference |
| [Documents/DEVNOTES.md](Documents/DEVNOTES.md) | Known issues and fixes |
| [Documents/TODO.md](Documents/TODO.md) | Planned features and backlog |
| [Documents/SAVE_SYSTEM.md](Documents/SAVE_SYSTEM.md) | Save/load system, setup steps, extending save data |
| [Documents/UNITY_COMPONENTS.md](Documents/UNITY_COMPONENTS.md) | Reference guide for all built-in Unity components |
| [Documents/SCENE_RULES.md](Documents/SCENE_RULES.md) | Per-scene gameplay overrides: inventory lock, combat toggles, DOT, movement lock || [Documents/EQUIPMENT.md](Documents/EQUIPMENT.md) | Equipment slots: EquipmentManager, EquipmentModel, ItemData bonus fields, EntityStats integration |
| [Documents/WEAPON_SWING.md](Documents/WEAPON_SWING.md) | Equipped weapon swing animation, CombatAttacker timing, scene wiring, and tuning |
| [Documents/NPC_MELEE_AI.md](Documents/NPC_MELEE_AI.md) | Wandering melee NPC behavior, proximity engagement, sword attacks, and scene wiring |
| [Documents/NPC_HEALTH_BARS.md](Documents/NPC_HEALTH_BARS.md) | Automatic enemy HP bars, Inspector tuning, and combat behavior |
| [Documents/CHARACTER_STATISTICS.md](Documents/CHARACTER_STATISTICS.md) | CharacterStatistics component: attack/kill/damage/item/money tracking, per-stat events, CombatAttacker integration |
| [Documents/WALLET.md](Documents/WALLET.md) | Spendable currency balance, add/spend/subtract API, transaction history, and save integration |
| [Documents/MARKET_ECONOMY.md](Documents/MARKET_ECONOMY.md) | Shared player/NPC trading architecture, atomic trades, market ledger, simulation, and persistence |
| [Documents/MANA_ECONOMY.md](Documents/MANA_ECONOMY.md) | Mana as shared currency/spell fuel, scarcity rules, faucets, sinks, capacity, and unification plan |
| [Documents/ECONOMIC_PROGRESSION.md](Documents/ECONOMIC_PROGRESSION.md) | Early/mid/late economic phases, rewards, smooth nested unlocks, and mana progression |
| [Documents/QUEST_PROGRESSION_INTEGRATION.md](Documents/QUEST_PROGRESSION_INTEGRATION.md) | How quest graphs and world-state facts drive nested NPC, location, transformation, and market unlocks |
| [Documents/ECONOMY_BALANCING_RULES.md](Documents/ECONOMY_BALANCING_RULES.md) | Soft economic resets, early difficulty, bounded RNG, economy workbook fields, and reward rules |
| [Documents/TRADE_SYSTEM.md](Documents/TRADE_SYSTEM.md) | Atomic player/NPC item-for-mana trades, participants, validation, ledger, persistence, and quest events |
| [Documents/WORLD_OBJECTS.md](Documents/WORLD_OBJECTS.md) | ItemPickup, WorldObject, IInteractable interface, InventoryHelper utility |
| [Documents/SLIDING_DOORS.md](Documents/SLIDING_DOORS.md) | Reusable E-interactable two-panel retracting gate, lock-state colors, and prefab setup |
| [Documents/KEY_HOLDER.md](Documents/KEY_HOLDER.md) | IKeyHolder contract, entity-based key resolution for doors, and door use results |
| [Documents/NPC_AI.md](Documents/NPC_AI.md) | NPC AI: NpcBehaviorBase + NpcPerception foundation, NpcPathfinder, NpcKeyring, NpcMemory, NpcUseDoorBehavior, and persisted lock knowledge |
| [Documents/MODEL_VIEW_SLICE.md](Documents/MODEL_VIEW_SLICE.md) | Model/view architecture: pure-C# Game.Core models (no UnityEngine), GameSession, Unity adapters, and the NPC health + logical-cell slice |
| [Documents/DOOR_PLACEMENT_BRUSH.md](Documents/DOOR_PLACEMENT_BRUSH.md) | Tile Palette brush for painting aligned functional sliding-door prefabs |
| [Documents/KEYRING.md](Documents/KEYRING.md) | Slot-free player key storage, inventory viewer, door/quest routing, and save integration |
| [Documents/ENEMY_LOOT_DROPS.md](Documents/ENEMY_LOOT_DROPS.md) | JSON-owned enemy loot, death cleanup, runtime loot piles, and pickup flow |
| [Documents/WORLD_STATE.md](Documents/WORLD_STATE.md) | World State System: WorldStateDB, WorldStateKey, all WorldState components, quest integration |
| [Documents/TRAINING_ARENA.md](Documents/TRAINING_ARENA.md) | Portal-linked training wing, key keeper, locked door, repeatable enemy spawner, and telegraphed dash enemy |
| [Documents/TWO_WORLD_SYSTEM.md](Documents/TWO_WORLD_SYSTEM.md) | Two world layers, portal character switching, remembered positions, scoped inventories, and save integration |
| [Documents/PLAYER_AVATARS.md](Documents/PLAYER_AVATARS.md) | Separate World A/World B avatar profiles, shared stats, per-world abilities, and generated player prefabs |
| [Documents/ISOMETRIC_CONVERSION.md](Documents/ISOMETRIC_CONVERSION.md) | MMBN-style isometric presentation: fixed camera, 2:1 diamond tilemaps, upright sprites, Y-sort, and the Overworld rename |
| [Documents/TILEMAP_RULES.md](Documents/TILEMAP_RULES.md) | Grid/Tilemap alignment rules: transforms must be at origin, place content via cells, and use the alignment validator |
| [Documents/DATA_VALIDATION.md](Documents/DATA_VALIDATION.md) | Editor validator for string-id data: blank/duplicate ids, dangling item/quest/node/portal references, dialogue links, and per-scene NPC ids |
| [Documents/ARCHITECTURE_AUDIT.md](Documents/ARCHITECTURE_AUDIT.md) | Audit of the Data/Presentation split: enforcement mechanisms, what is separated, remaining authoritative state in Presentation, and migration order |
| [Documents/NPC_STRESS_TEST.md](Documents/NPC_STRESS_TEST.md) | Disposable 25/50/100-NPC wandering + pathfinding performance harness (Tools > Stress) and its results |

---

## Repository Layout

`Assets/Scripts` is split into a **data** layer and a **presentation** layer, each with its own
assembly definition:

| Folder | Assembly | Contains |
|---|---|---|
| `Assets/Scripts/GameData/` | `Game.Data` (`noEngineReferences: true`) | Pure C# domain: models, value types, interfaces, services, and save DTOs. **No UnityEngine.** |
| `Assets/Scripts/GamePresentation/` | `Game.Presentation` | All MonoBehaviours, UI, adapters, controllers, views, and content definitions (ScriptableObjects). |
| `Assets/Scripts/GamePresentation/Editor/` | `Game.Presentation.Editor` (Editor-only) | Editor tools and scene/prefab builders. |

Rules:

1. If it uses UnityEngine (MonoBehaviour, ScriptableObject, Vector2, etc.), it belongs in
   `GamePresentation`.
2. Authoritative saveable state belongs in `GameData` as a plain C# model. See
   [Documents/MODEL_VIEW_SLICE.md](Documents/MODEL_VIEW_SLICE.md).
3. `GameData` must never reference `Game.Presentation` or UnityEngine — the compiler enforces this
   via `noEngineReferences`.
4. Data models keep the `Game.Core` C# namespace even though the assembly is `Game.Data`.
5. Move scripts with their `.meta` files so Unity GUID references (scenes, prefabs) survive.
6. Gameplay **tuning/stat values** belong in a `[Serializable]` config class in `GameData`
   (e.g. `PlayerMovementConfig`, `CombatAttackerConfig`). The MonoBehaviour holds a
   `[SerializeField]` config field and reads it; it may keep only Unity-only references
   (Transform, LayerMask, SpriteRenderer). Unity-native value types are stored in pure form
   (KeyCode as int, Color as RGBA floats).
7. Engine-free domain logic lives in `GameData` behind a plain-C# contract. `InventoryModel` and
   `InventorySlot` operate on `IItem`; the Unity `ItemData` ScriptableObject implements `IItem` and
   keeps presentation-only data (icon, equipment slot, use effects). Reach that data in
   `GamePresentation` with `IItem.AsItemData()` / `ItemExtensions` — never leak UnityEngine into
   `GameData`. `ItemType`, `ItemFlags`, and `ItemScope` live in `GameData` for the same reason.

## Architecture: Engine-Free Core

**"Engine-Free Core"** is the name of this project's architecture. Two layers and one rule:

| Layer | Assembly | May reference | Owns |
|---|---|---|---|
| **Core** | `Game.Data` (`noEngineReferences: true`) | BCL only | Authoritative state, models, services, tuning config, save DTOs, validation |
| **Shell** | `Game.Presentation` / `Game.Presentation.Editor` | `Game.Data`, UnityEngine | MonoBehaviours, UI, adapters, content assets (`ScriptableObject`) — wiring, references, rendering |

**The rule: Core owns state; the Shell displays and drives it.** If a value is saved, its authority
belongs in `Game.Data`. A MonoBehaviour may hold Unity-only references (`Transform`, `Sprite`,
`LayerMask`) and presentation cache — never the source of truth.

When a component must remain (inspector fields, prefab identity, `DontDestroyOnLoad`), keep the
MonoBehaviour as a thin **facade**: it owns the serialized fields and lifetime and forwards all
logic to a plain-C# Core type. Examples: `Wallet` → `ManaAccount`, `WorldStateManager` →
`WorldFacts`, `NpcStateView` → `NpcState`.

Rules for new systems:

1. Put state and rules in a plain C# type under `Assets/Scripts/GameData/` (namespace `Game.Core`).
2. Put the MonoBehaviour adapter in `Assets/Scripts/GamePresentation/`.
3. Tuning lives in a `[Serializable]` config class in `GameData` (see Repository Layout rule 6);
   Unity value types are stored in pure form (KeyCode as int, Color as RGBA floats).
4. No `UnityEngine` type may enter `GameData` — the build enforces this via `noEngineReferences`.
5. When a Core type needs engine data, pass it in as an interface implemented by the adapter
   (`IItem` ⇄ `ItemData`, `IHealthModel` ⇄ `EntityStats`). Do not break the boundary to save time.
6. Prefer engine-free tests in `Assets/Tests/EditMode/` (mirrored by `dotnet test`); use
   `Assets/Tests/Presentation/` only for genuinely Unity-bound behaviour.
7. Run `Tools/verify-all.ps1`. See [Documents/ARCHITECTURE_AUDIT.md](Documents/ARCHITECTURE_AUDIT.md)
   for the current conformance state and remaining migrations.

## Known Hazards

### `WorldTravelState` is a process-global singleton

`WorldTravelState` is `DontDestroyOnLoad`, so `CurrentWorld` persists for the whole play session,
and `WorldCharacter.SetActiveForWorld` activates only the character whose world matches — a mismatch
**deactivates the player**.

- Any code path that enters a scene outside the normal portal flow — a fast-travel/menu "return", a
  new-game flow, a manual `SceneLoader` call, or a direct `LoadScene` — must establish the world
  (`WorldTravelState.Instance.SetCurrentWorld(...)`) or rely on the scene's `WorldSceneIdentity`.
- Every scene containing a `WorldCharacter` must have **exactly one** `WorldSceneIdentity`. The data
  validator (`Tools > Validation > Validate Game Data`) enforces this.
- Play Mode tests inherit `Assets/Tests/PlayMode/PlayModeTestBase.cs`, which resets the world to
  World A before each test for the same reason.

## Safety Rules

1. Do not edit Unity scene or prefab files (`*.unity`, `*.prefab`) unless the user explicitly asks for that exact change in the current request.
2. Default to script-only changes for gameplay updates.
3. If a task would require scene edits, stop and ask for confirmation first.
4. Do not use Unity **UI or computer-use automation** to drive the Editor. Use the API-based automation in **Unity Editor Automation** below instead.

## Unity Editor Automation

Primary channel: the official **Unity CLI + Pipeline package** (`com.unity.pipeline`).

- Binary: `C:\Users\Kevin\AppData\Local\Unity\bin\unity.exe` (winget/PATH on install; call by full path until a terminal restart).
- Verify the connection: `unity pipeline list` (expect a Server Port and Server Reachable = true).
- List commands: `unity command` (built-ins plus project custom commands).
- Common calls:
  - `unity command editor_status`
  - `unity command capture_scene_view --width 1280 --height 720 --save_path Assets/LLM/Bridge/Screenshots/sceneview.png`
  - `unity command capture_game_view --source screen --save_path Assets/.../gameview.png` (includes overlay UI; Play Mode only)
  - `unity command console --tail 50`
  - `unity command <name> --non-interactive`
- Capture `save_path` values must be inside the project root.
- `eval` / `eval_file` execute arbitrary C# against the live Editor — treat like remote code execution and keep it local.
- The Pipeline package is local-development tooling; it is not shipped with the game.
- Bridge fallback: the file-based Unity Bridge (`unity-cmd.ps1`, `Assets/LLM/Bridge/`) remains available if the CLI server is down.
- Scene and prefab edits still require an explicit request (see Safety Rules above).

## Verification

Run the full suite in one command before considering a change done:

```
powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1
```

It runs, in order: Unity compile check, Unity Edit Mode tests, Unity Play Mode tests (async),
`dotnet test`, then a scene smoke test. It exits non-zero if any step fails.

Test assemblies:

| Path | Kind | Scope |
|---|---|---|
| `Assets/Tests/EditMode/` | Edit Mode, engine-free | Pure `Game.Data` model + config defaults. Mirrored by `dotnet test`. |
| `Assets/Tests/Presentation/` | Edit Mode | Reflection guards, save-data round trip, economy conservation (references `Game.Presentation`). |
| `Assets/Tests/PlayMode/` | Play Mode | Loads real scenes and asserts runtime wiring. |

Play Mode tests must run async (`run_tests --mode PlayMode --async_tests`, then poll
`test_status`); a synchronous request is dropped by the domain reload. Play Mode tests share one
play session, so `DontDestroyOnLoad` singletons (`WorldTravelState`, `GameSessionHost`,
`PlayerKeyring`) persist between tests. Play Mode test fixtures should inherit
`Assets/Tests/PlayMode/PlayModeTestBase.cs`, which restores `WorldTravelState` to World A before each
test (otherwise a prior test that loaded World B deactivates the World A player). Reset any other
state a test depends on rather than assuming a clean scene. The scene smoke test
(`Tools/verify-smoke.ps1`) is the layer that catches runtime/serialization errors such as
duplicate serialized field names, so do not skip it.

## Documentation Rules

1. Whenever a new feature or system is implemented, create a corresponding `.md` file in the `Documents/` folder explaining what it does and how to set it up in Unity.
2. The document must cover: what components/scripts to add, which GameObjects they go on, how to wire them up in the Inspector, and any required scene setup steps.
3. After creating the document, add a row for it in the **Reference Documents** table at the top of this file.

## Prefab Graph Rules

Whenever a change affects the component layout of any prefab (adding/removing components, changing key Inspector fields, adding new prefabs, or changing component relationships), update both:
1. **`Documents/PREFAB_GRAPH.md`** — edit the Mermaid `flowchart` source to reflect the new structure.
2. **`Documents/PREFAB_GRAPH.svg`** — regenerate the SVG by rendering the updated Mermaid diagram and replacing the file contents.

Changes that require a graph update include (but are not limited to):
- A new `[RequireComponent]` or auto-`AddComponent` relationship.
- A new serialized field that references another component/prefab shown in the graph.
- Adding or removing a whole component from a prefab.
- Changing a key label shown as node text (e.g. `usePlayerInput`, `NpcType`, `Gravity`).
- Adding an entirely new prefab archetype that belongs in the graph.

## Tilemap Rules

1. Every `Grid` and `Tilemap` GameObject must have local position `(0, 0, 0)`, rotation
   identity, and scale `(1, 1, 1)`. Express all placement through **cell coordinates**
   (`Tilemap.SetTile`, `Grid.CellToWorld`), never through transform offsets or scaling.
2. This applies to scene tilemaps **and** tilemap palette prefabs. An offset on one tilemap
   shifts it out of alignment with every other tilemap and every painted prefab sharing the
   same cells.
3. Code that creates Grids/Tilemaps (for example `WorldBSceneBuilder`, `TrainingArenaBuilder`)
   must leave the new transform at its default and paint tiles at cells only.
4. Run **Tools > World > Tilemap Alignment > Validate Open Scene** before saving scene changes.
   Use **Normalize Open Scene** to bake a stray offset into cell coordinates and zero the
   transform. See [Documents/TILEMAP_RULES.md](Documents/TILEMAP_RULES.md).

## Editor Preview Rules

1. A component that generates, positions, or resizes its own visual children at runtime
   (gates/doors, portals, spawners, markers, etc.) **must also build that preview in the
   editor**. Never generate visuals in `Awake` only.
2. Implement the preview with `[ExecuteAlways]` plus a guarded `OnEnable` / `OnValidate` build,
   or an editor utility. Rebuild only when the relevant settings change so the preview does not
   churn or mark the scene dirty every frame.
3. A designer must be able to see the object in the Scene view and position it **without
   entering Play mode**. If the object is invisible in the editor, the rule is not met.
4. Guard the editor build so it is safe when runtime dependencies are missing (for example no
   `Grid` yet) and skip prefab-asset stages where there is no scene context. At runtime the same
   build code runs; editor and play mode must not diverge.
5. Prefer keeping the generated children serialized in the scene/prefab so the preview persists
   even before scripts run.

Reference implementation: `SlidingDoor` builds its gate cells with `[ExecuteAlways]` in both
edit and play mode. See [Documents/SLIDING_DOORS.md](Documents/SLIDING_DOORS.md).

## Scripting Rules

1. Do not use `IReadOnlyList<T>` anywhere in the codebase. Use an appropriate concrete collection type or another API shape instead.
2. Unity message methods (`Update`, `FixedUpdate`, `LateUpdate`, `OnGUI`, `Awake`, `Start`, `OnEnable`, `OnDisable`, `OnValidate`, ...) are event listeners that Unity calls even when the body is empty, and every call crosses the scripting-perimeter from native. Never declare one the class does not use — after every refactor, delete the ones that no longer have work to do. This also applies to declaring them "as a placeholder for later".

Every new C# script file must begin with a `/// <summary>` XML doc comment block directly above the class (or above its `[Attribute]` lines). The comment must cover:

1. **What it does** — one or two sentences explaining the script's responsibility.
2. **Unity setup** — step-by-step instructions for wiring it up in the Unity Editor:
   - Which GameObject to attach it to.
   - Required or auto-added sibling components (`[RequireComponent]` or manual).
   - Every Inspector-visible field that the user must assign or configure.
   - Any scene hierarchy requirements (e.g. must be inside a Canvas, must be on a DontDestroyOnLoad object).
3. **Runtime API** — public methods or events that other scripts call, if any.
4. If the script is a pure C# class (no `MonoBehaviour`), note "Unity setup: none" and describe how it is created and accessed instead.

Example format:
```csharp
/// <summary>
/// One-line description of what this component does.
///
/// Unity setup:
///   1. Add to [which GameObject].
///   2. Assign [field] to [what].
///   3. Requires [Component] (added automatically / add manually).
///
/// Runtime API:
///   MyClass.Instance.DoSomething();
/// </summary>
```

## Game Basics

This project is a **2D top-down** game.

### Core Rules
1. Perspective: **Top-down** (player moves on X/Y plane)
2. Dimension: **2D**

### Player Setup (Top-Down)
- Add `Rigidbody2D` to Player
  - Body Type: Dynamic
  - Gravity Scale: 0
  - Freeze Rotation Z: enabled
- Add a collider (`BoxCollider2D` or `CapsuleCollider2D`)
- Add `Assets/Scripts/GamePresentation/Player/PlayerController2D.cs`.

### Input
- Movement: `WASD` or Arrow Keys
- Uses Unity axes:
  - `Horizontal`
  - `Vertical`

### Scene Setup
- World objects should use `Collider2D` components.
- If objects need physics movement, add `Rigidbody2D`.
- Camera is usually orthographic for top-down 2D.

### Notes
- No jump logic is needed for this controller.
- Movement is normalized so diagonal speed is not faster than straight movement.
- Do not add on-screen gameplay instructions or control hints for the player. Interaction and mechanics should be discoverable through play without hand-holding.

### Common Issues
- Not moving: script not attached, missing `Rigidbody2D`, or speed is 0
- Falling: gravity scale is not 0
- Spinning: rotation not frozen
- Fast diagonal movement: input not normalized

See [Documents/PLAYER.md](Documents/PLAYER.md) for full player system documentation.

## HP and Mana System

### Overview
- `EntityStats.cs` — shared by the player and enemy NPCs. Tracks HP and exposes the compatible MP/mana API with events.
- `Wallet.cs` — owns the player's canonical mana balance and capacity for both trade and spellcasting.
- `EntityStatsUI.cs` — listens to `EntityStats` events and drives two `Image` fills in a Canvas.

### Player
- `PlayerController2D` has `[RequireComponent(typeof(EntityStats))]`, so the component is always present.
- Configure HP on `EntityStats`. If no Wallet is manually present, its `Max Mp` and `Starting Mp` values initialize the Wallet that `PlayerController2D` adds at runtime.

### Enemy NPCs
- Set `Npc Type = Enemy` on any `NpcController`. `Awake()` automatically calls `AddComponent<EntityStats>()` and `Configure(enemyMaxHp)`.
- Tune `Enemy Max Hp` per prefab in the Inspector.
- Access via `npcController.Stats` (returns `null` for non-enemy NPCs).

### Key Methods
| Method | Description |
|---|---|
| `TakeDamage(int)` | Reduces HP; fires `OnDeath` when HP reaches 0 |
| `Heal(int)` | Restores HP up to `maxHp` |
| `SpendMp(int)` | Delegates to the bound Wallet and records a spell debit; returns false if insufficient |
| `RestoreMp(int)` | Restores canonical mana up to Wallet capacity |
| `Configure(int hp, int mp)` | Sets stats at runtime after `AddComponent` |
| `IncreaseMaxHp/Mp(int)` | Scales max stat (e.g. on level-up) |

### Events
- `OnHpChanged(int current, int max)` — fired on any HP change
- `OnMpChanged(int current, int max)` — fired on any MP change
- `OnDeath` — fired once when HP hits 0

### UI Setup
1. Create a Canvas (Screen Space – Overlay).
2. For each bar: add a background `Image` and a child "Fill" `Image` with `Image Type = Filled`, `Fill Method = Horizontal`.
3. Assign the Fill Images (not the backgrounds) to `hpFill` / `mpFill` on `EntityStatsUI`.
4. Assign `Entity Stats` or leave it null — the component will auto-find one via `FindAnyObjectByType<EntityStats>()`.

See [Documents/STATS.md](Documents/STATS.md) for full stats system documentation.
