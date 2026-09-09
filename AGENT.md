# AGENT.md

## Reference Documents

Full documentation for each system lives in the `Documents/` folder. Read the relevant file before making changes to that system.

| Document | Contents |
|---|---|
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
| [Documents/SCENE_RULES.md](Documents/SCENE_RULES.md) | Per-scene gameplay overrides: inventory lock, combat toggles, DOT, movement lock |
| [Documents/EQUIPMENT.md](Documents/EQUIPMENT.md) | Equipment slots: EquipmentManager, EquipmentModel, ItemData bonus fields, EntityStats integration |
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
| [Documents/SLIDING_DOORS.md](Documents/SLIDING_DOORS.md) | Reusable E-interactable single-panel sliding door and prefab setup |
| [Documents/KEYRING.md](Documents/KEYRING.md) | Slot-free player key storage, inventory viewer, door/quest routing, and save integration |
| [Documents/ENEMY_LOOT_DROPS.md](Documents/ENEMY_LOOT_DROPS.md) | JSON-owned enemy loot, death cleanup, runtime loot piles, and pickup flow |
| [Documents/WORLD_STATE.md](Documents/WORLD_STATE.md) | World State System: WorldStateDB, WorldStateKey, all WorldState components, quest integration |
| [Documents/TRAINING_ARENA.md](Documents/TRAINING_ARENA.md) | Portal-linked training wing, key keeper, locked door, repeatable enemy spawner, and telegraphed dash enemy |

---

## Safety Rules

1. Do not edit Unity scene or prefab files (`*.unity`, `*.prefab`) unless the user explicitly asks for that exact change in the current request.
2. Default to script-only changes for gameplay updates.
3. If a task would require scene edits, stop and ask for confirmation first.
4. Do not use Unity UI automation or computer-use automation to control the Unity Editor. Make project changes through files and provide manual Unity verification steps when editor interaction is required.

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

## Scripting Rules

1. Do not use `IReadOnlyList<T>` anywhere in the codebase. Use an appropriate concrete collection type or another API shape instead.

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
- Add `Assets/Scripts/Player/PlayerController2D.cs`.

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
