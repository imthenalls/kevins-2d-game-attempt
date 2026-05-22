# Stats System (HP / MP)

## Overview

`EntityStats` is a single component used by both the player and enemy NPCs to track HP and MP. It fires C# events on every change so any number of listeners (UI, AI, quest system) can react without polling.

---

## EntityStats

**File:** `Assets/Scripts/Entity/EntityStats.cs`

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Max Hp | 100 | Maximum hit points |
| Starting Hp | 100 | HP on `Awake` (clamped to `maxHp`) |
| Max Mp | 50 | Maximum mana points |
| Starting Mp | 50 | MP on `Awake` (clamped to `maxMp`) |

Inspector values are used when the component is placed manually. For runtime-spawned entities (enemies via `NpcController.Awake`), call `Configure()` instead — it bypasses the inspector values.

### Events

```csharp
event Action<int, int> OnHpChanged   // (currentHp,  maxHp)  — fires on any HP change
event Action<int, int> OnMpChanged   // (currentMp,  maxMp)  — fires on any MP change
event Action           OnDeath       // fires once when HP reaches 0
```

### HP methods

| Method | Description |
|---|---|
| `TakeDamage(int amount)` | Reduces HP; fires `OnDeath` if HP hits 0. No-op if already dead. |
| `Heal(int amount)` | Restores HP up to `maxHp`. No-op if dead. |
| `SetHp(int value)` | Direct set, clamped to `[0, maxHp]`. |
| `IncreaseMaxHp(int, bool healDelta)` | Raises `maxHp`; optionally heals the added amount (default: true). |

### MP methods

| Method | Returns | Description |
|---|---|---|
| `SpendMp(int cost)` | `bool` | Returns `true` and deducts; `false` if insufficient (MP unchanged). |
| `RestoreMp(int amount)` | — | Restores MP up to `maxMp`. |
| `SetMp(int value)` | — | Direct set, clamped to `[0, maxMp]`. |
| `IncreaseMaxMp(int, bool restoreDelta)` | — | Raises `maxMp`; optionally restores the added amount (default: true). |

### Runtime configuration

```csharp
// After AddComponent — sets all four values at once and skips Awake init
stats.Configure(hp: 30, mp: 0);  // mp defaults to 0 (enemy, no mana)
stats.Configure(hp: 100, mp: 50); // player
```

### Read-only properties

```csharp
int  stats.Hp      // current HP
int  stats.Mp      // current MP
int  stats.MaxHp
int  stats.MaxMp
bool stats.IsAlive // Hp > 0
```

---

## PlayerStatsUI

**File:** `Assets/Scripts/PlayerStatsUI.cs`

Subscribes to `EntityStats` events and drives two `Image` fill amounts (0–1).

### Inspector fields

| Field | Description |
|---|---|
| Player Stats | `EntityStats` reference. Auto-found via `FindFirstObjectByType` if null. |
| Hp Fill | The Fill `Image` for the HP bar |
| Mp Fill | The Fill `Image` for the MP bar |

### Canvas setup

```
Canvas
  └── HPBar
        ├── Background (Image)
        └── Fill (Image)   ← Image Type = Filled, Fill Method = Horizontal
  └── MPBar
        ├── Background (Image)
        └── Fill (Image)   ← Image Type = Filled, Fill Method = Horizontal
  └── PlayerStatsUI        ← assign the two Fill images here
```

---

## Where Each Component Lives

| Entity | Component | How added |
|---|---|---|
| Player | `EntityStats` | Auto via `[RequireComponent]` on `PlayerController2D` |
| Enemy NPC | `EntityStats` | Auto via `NpcController.Awake()` when `Npc Type = Enemy` |
| UI | `PlayerStatsUI` | Manual — add to Canvas GameObject |

---

## Usage Examples

```csharp
// Deal damage
GetComponent<EntityStats>().TakeDamage(25);

// Gate an ability on MP cost
if (stats.SpendMp(20))
    CastFireball();

// Subscribe to death
stats.OnDeath += HandleDeath;

// Level-up: grow both pools
stats.IncreaseMaxHp(10);   // heals 10 too
stats.IncreaseMaxMp(5);    // restores 5 too

// Access enemy stats via NpcController
npcController.Stats?.TakeDamage(10);
```
