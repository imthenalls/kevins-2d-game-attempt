# World State System

## Overview

The World State System is the single source of truth for all persistent world progression in the game. It records what has happened in the world (flags, counters, values) and allows any scene object, NPC, enemy, or quest to react to those facts without polling every frame or hardcoding cross-system dependencies.

### Architecture at a Glance

```
WorldStateDB (singleton, DontDestroyOnLoad)
  ├── Stores Dictionary<string, object> of all facts
  ├── Fires WorldStateDB.OnFlagChanged(key) on every mutation
  └── Automatically saved/loaded by SaveManager (zero manual setup)

World State Components (MonoBehaviours — designer-placed in scenes)
  ├── WorldStateActivator      — show/hide GameObjects
  ├── WorldStateDestroyer      — permanently destroy objects
  ├── WorldStateSpawner        — instantiate prefabs
  ├── WorldStateInteractable   — gate IInteractable components
  ├── WorldStateDialogueSelector — swap NPC dialogue graphs
  ├── WorldStateNpcReactor     — move NPCs, change behavior state
  └── EnemyDeathFlagSetter     — set a flag when an enemy dies

WorldStateKey (ScriptableObject)
  └── Designer-friendly named key asset — drag instead of typing strings
```

All world state changes **automatically save** because `SaveManager` already serializes all `WorldStateDB` facts on every `Save()` call. No custom save logic is ever needed.

---

## WorldStateDB — Core API

`WorldStateDB` is the existing singleton that has been extended with events, flag convenience methods, and typed accessors. It lives on a `DontDestroyOnLoad` bootstrap GameObject.

### Boolean Flag API

| Method | Description |
|---|---|
| `SetFlag(string key)` | Mark a flag as set (stores `true`) |
| `ClearFlag(string key)` | Remove a flag (HasFlag returns false) |
| `ToggleFlag(string key)` | Set if absent, clear if present |
| `HasFlag(string key)` | True when key exists and is truthy |

### Typed Value API

| Method | Description |
|---|---|
| `SetInt(key, int)` | Store an integer |
| `GetInt(key, fallback)` | Read an integer (fallback if missing) |
| `SetFloat(key, float)` | Store a float |
| `GetFloat(key, fallback)` | Read a float |
| `SetString(key, string)` | Store a string |
| `GetString(key, fallback)` | Read a string |

### Low-Level Fact API (used by quest system)

| Method | Description |
|---|---|
| `SetFact(key, object)` | Write any value type |
| `GetFact(key)` | Read as raw object (null if absent) |
| `HasFact(key)` | True if key exists (any value) |
| `ClearFact(key)` | Remove any fact |

### Event

```csharp
// Subscribe in OnEnable, unsubscribe in OnDisable:
WorldStateDB.OnFlagChanged += key => Debug.Log($"{key} changed");
```

Fired whenever any fact is set or cleared at runtime. **Not fired during `LoadSnapshot`** (save restore) — components use `Start()` for initial state evaluation.

### HasFlag compatibility

`HasFlag` accepts flags set via any method:
- `SetFlag(key)` → stores `bool true` → `HasFlag` returns `true`
- `SetFact(key, "True")` (quest JSON) → `HasFlag` returns `true`
- `SetFact(key, "False")` → `HasFlag` returns `false`
- `ClearFlag(key)` / missing key → `HasFlag` returns `false`

---

## WorldStateKey — Designer-Friendly Keys

Instead of typing raw strings in component fields (which are error-prone), create `WorldStateKey` ScriptableObject assets.

### Setup

1. Right-click in the Project window → **Create → Game → World State Key**.
2. Name the asset after its key (e.g. `WSKey_Boss.GoblinKing.Defeated`).
3. Set the **Key** field to the exact string (e.g. `Boss.GoblinKing.Defeated`).
4. Drag this asset into any World State component's **Flag Key** field.

### Naming Convention

Use dot-notation: `Category.Subject.State`

```
Boss.GoblinKing.Defeated
Quest.Main.001Completed
Village.BlacksmithUnlocked
Bridge.Fixed
Merchant.Rescued
Enemy.cave_spider.Defeated
Chest.Dungeon1.Room3.Opened
Key.GardenGate.Obtained
```

All components also have a **Raw Key** string fallback for rapid prototyping without creating assets.

---

## World State Components

### WorldStateActivator

**File:** `Assets/Scripts/WorldState/WorldStateActivator.cs`

Enables or disables a target GameObject whenever a world state flag changes.

| Field | Description |
|---|---|
| Flag Key | WorldStateKey asset (or Raw Key fallback) |
| Invert | Active while flag is ABSENT instead of present |
| Target | GameObject to control (defaults to self if blank) |

**Setup:** Add to any scene GameObject. Assign a Flag Key.

**Examples:**
- Bridge becomes visible after `Bridge.Fixed` is set.
- Merchant NPC is hidden until `Merchant.Rescued` is set.
- Pre-fight trigger is hidden after `Boss.GoblinKing.Defeated` (Invert = true).

---

### WorldStateDestroyer

**File:** `Assets/Scripts/WorldState/WorldStateDestroyer.cs`

Permanently destroys this GameObject when a flag is set. On the next scene load the object destroys itself immediately (before the player sees it) if the flag is already set.

| Field | Description |
|---|---|
| Flag Key | WorldStateKey asset (or Raw Key fallback) |

**Setup:** Add to a chest, crate, boss, or any one-time scene object.

**Examples:**
- Boss corpse disappears after `Enemy.cave_spider.Defeated`.
- Opened chest prop is removed after `Chest.Dungeon1.Boss.Opened`.

**Pairs with EnemyDeathFlagSetter:** place both on the same boss — the flag setter fires on death, the destroyer removes the body.

---

### WorldStateSpawner

**File:** `Assets/Scripts/WorldState/WorldStateSpawner.cs`

Instantiates a prefab when a world state condition is met. Spawns once and tracks the instance.

| Field | Description |
|---|---|
| Flag Key | WorldStateKey asset (or Raw Key fallback) |
| Spawn When Flag Absent | Spawn while flag is NOT set (inverts condition) |
| Prefab | The prefab to instantiate |
| Spawn Point | Override position/rotation (defaults to this transform) |
| Destroy On Condition False | Remove instance when condition becomes false |

**Setup:** Place an empty GameObject at the spawn location. Assign the Flag Key and Prefab.

**Examples:**
- `Merchant.Rescued` set → merchant NPC prefab appears in town.
- `BanditCamp.Cleared` set + Spawn When Flag Absent OFF → enemy never spawns.

---

### WorldStateInteractable

**File:** `Assets/Scripts/WorldState/WorldStateInteractable.cs`

Gates `IInteractable` MonoBehaviour components by enabling/disabling them. Since `WorldObject.CanInteract()` checks `component.enabled`, a disabled WorldObject is invisible to the player.

| Field | Description |
|---|---|
| Flag Key | WorldStateKey asset (or Raw Key fallback) |
| Require Flag Absent | Allow interaction while flag is ABSENT |
| Guarded Components | MonoBehaviours (IInteractable) to enable/disable |

**Setup:**
1. Add WorldStateInteractable to the same GameObject as a WorldObject.
2. Drag the WorldObject into Guarded Components.
3. Assign the flag condition.

For a "locked" message: add a second WorldObject with locked text + WorldStateActivator (Invert = true) to show it only when locked.

**Examples:**
- Garden gate WorldObject enabled after `Key.GardenGate.Obtained`.
- Treasure chest enabled after `Boss.GoblinKing.Defeated`.

---

### WorldStateDialogueSelector

**File:** `Assets/Scripts/WorldState/WorldStateDialogueSelector.cs`

Selects which `DialogueGraphAsset` an `NpcDialogue` uses based on world state flags. Entries are checked top-to-bottom; the first matching entry wins.

| Field | Description |
|---|---|
| Entries | List of (Flag Key + Dialogue Asset) pairs, checked in order |
| Default Dialogue | Used when no entry condition is met |

Each entry has:

| Field | Description |
|---|---|
| Flag Key | WorldStateKey asset (or Raw Key fallback) |
| Require Flag Absent | Match when flag is ABSENT |
| Dialogue | DialogueGraphAsset to select |

**Setup:**
1. Add WorldStateDialogueSelector to an NPC that has NpcDialogue.
2. Clear the Dialogue Asset / Dialogue Id fields on NpcDialogue (this component manages them).
3. Add entries with conditions and corresponding DialogueGraphAsset references.
4. Assign a Default Dialogue.

**Examples:**
```
NPC: Mayor of Willowmere

Entry 0:  Quest.Main.002Completed set → "Thank you for saving the village!"
Entry 1:  Quest.Main.002Started set   → "You're still working on it?"
Default:                               → "Things have been troubled lately."
```

---

### WorldStateNpcReactor

**File:** `Assets/Scripts/WorldState/WorldStateNpcReactor.cs`

Moves an NPC to a new position and/or changes its behavior state in response to flags. Reactions are one-way (applied when condition becomes true) unless an Invert Condition entry provides the reverse.

| Field | Description |
|---|---|
| Reactions | List of flag-driven reactions |

Each reaction has:

| Field | Description |
|---|---|
| Flag Key | WorldStateKey asset (or Raw Key fallback) |
| Invert Condition | Fire when flag is ABSENT |
| Move To | Teleport the NPC here when condition is met |
| Change Behavior State | Override NPC behavior state |
| Behavior State | State to apply (`Idle`, `Disabled`, `Talking`) |

**Setup:**
1. Add WorldStateNpcReactor to an NPC that has NpcController.
2. Add one reaction per flag that should affect this NPC.
3. Assign a Move To transform if the NPC should relocate (place an empty GameObject at the target spot).

**Examples:**
- `Merchant.Rescued` set → NPC moves to TownSquare position.
- `Village.Siege.Started` set → NPC behavior becomes `Disabled` (frozen).
- `Village.Siege.Ended` set + Invert OFF → NPC behavior returns to `Idle`.

---

### EnemyDeathFlagSetter

**File:** `Assets/Scripts/WorldState/EnemyDeathFlagSetter.cs`

Sets a world state flag automatically when an enemy is killed. Requires `CombatReceiver` on the same object.

| Field | Description |
|---|---|
| Flag Key | WorldStateKey asset (highest priority) |
| Raw Key | Fallback string key |
| Auto Generate Key | Derives `Enemy.<NpcId>.Defeated` from NpcController |

**Setup:**
1. Add to an enemy (or boss) that has CombatReceiver.
2. Enable Auto Generate Key for standard enemies, or assign a WorldStateKey asset for bosses with a meaningful key.

**Auto-generated key examples:**
```
NpcId: "goblin_king"   → "Enemy.goblin_king.Defeated"
NpcId: "cave_spider"   → "Enemy.cave_spider.Defeated"
```

**Typical boss setup:**

```
Boss (GameObject)
  ├── NpcController  (NpcType = Enemy, NpcId = "goblin_king")
  ├── CombatReceiver
  ├── EnemyDeathFlagSetter  (flagKey = WSKey_Boss.GoblinKing.Defeated)
  └── WorldStateDestroyer   (flagKey = WSKey_Boss.GoblinKing.Defeated)
```

---

## Quest Integration

Set world flags from quest nodes using the built-in action types in quest JSON:

```json
{
  "id": "quest_complete",
  "onEnterActions": [
    { "type": "SetFact",    "key": "Quest.RescueMerchant.Completed", "value": "True" },
    { "type": "SetFact",    "key": "Merchant.Rescued",                "value": "True" },
    { "type": "SetFact",    "key": "Village.BlacksmithUnlocked",       "value": "True" }
  ]
}
```

Two new action types are also available:

| Type | JSON | Description |
|---|---|---|
| `ClearFlag` | `{ "type": "ClearFlag", "key": "..." }` | Removes a flag |
| `ToggleFlag` | `{ "type": "ToggleFlag", "key": "..." }` | Flips a boolean flag |

Conditions in quest JSON can gate transitions on world state:

```json
{ "type": "Fact", "key": "Boss.GoblinKing.Defeated", "value": "True" }
```

---

## Save / Load

No additional setup is required. `SaveManager` already serializes all `WorldStateDB` facts in every `Save()` call. On `Load()`, facts are restored before the scene loads, so all World State Components evaluate correctly in their own `Start()`.

The event `WorldStateDB.OnFlagChanged` is **suppressed during `LoadSnapshot`** to prevent components from reacting to a bulk restore (they read the loaded state in `Start()` instead).

---

## Extending the System

### Adding a new World State Component

1. Subscribe to `WorldStateDB.OnFlagChanged` in `OnEnable`.
2. Unsubscribe in `OnDisable`.
3. Call `Refresh()` in `Start()` to apply the initial state.
4. In `Refresh()`, call `WorldStateDB.Instance.HasFlag(Key)` to read the current state.

```csharp
private void OnEnable()  => WorldStateDB.OnFlagChanged += HandleFlagChanged;
private void OnDisable() => WorldStateDB.OnFlagChanged -= HandleFlagChanged;
private void Start()     => Refresh();

private void HandleFlagChanged(string changedKey)
{
    if (changedKey == Key) Refresh();
}
```

### Adding a new Quest Action Type

1. Implement `IQuestAction` in `IQuestAction.cs`.
2. Register the type string in `QuestLoader.BuildAction()`.
3. Update the `QuestActionData` doc comment in `QuestData.cs`.

### Future systems that can build on WorldStateDB

- **NPC Schedules** — NPCs at different locations based on time-of-day flags.
- **Reputation** — `SetInt("Reputation.Village", n)` drives NPC reactions.
- **Achievements** — Check flags to unlock achievements without custom tracking.
- **Dynamic Events** — `WorldStateSpawner` with `spawnWhenFlagAbsent` covers conditional events.
- **Alternate Endings** — Quest conditions check flags set throughout the game.
- **Branching Quests** — `FactCondition` in quest transitions already reads WorldStateDB.
