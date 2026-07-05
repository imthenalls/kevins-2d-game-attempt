# Character Statistics System

## Overview

`CharacterStatistics` is a MonoBehaviour that tracks cumulative gameplay stats for any character that has a `CombatAttacker`. It subscribes to attacker events automatically — no polling or manual wiring needed. External systems (achievements, UI, save/load) subscribe to per-stat change events.

---

## Scripts

| Script | Location |
|---|---|
| `CharacterStatistics.cs` | `Assets/Scripts/Entity/` |

---

## Unity Setup

1. Add `CharacterStatistics` to any GameObject that has a `CombatAttacker` (player, enemy, etc.).
2. No Inspector fields to configure — the component finds and wires to `CombatAttacker` in `Awake`.
3. For stats sourced outside `CombatAttacker` (crits, kills from DoT, currency), call the public Record methods directly.

---

## Tracked Stats

| Property | Type | Source |
|---|---|---|
| `TotalAttacks` | `int` | Auto — `CombatAttacker.OnAttackLanded` |
| `TotalDamageDealt` | `int` | Auto — `CombatAttacker.OnAttackLanded` (damage arg) |
| `TotalKills` | `int` | Auto — `CombatAttacker.OnKillLanded`; or `RecordKill()` |
| `CriticalHits` | `int` | Manual — `RecordCriticalHit()` |
| `TotalItemsGathered` | `int` | Auto via `InventoryHelper.GiveItem`; or `RecordItemGathered(count)` |
| `TotalMoneyGained` | `int` | Manual — `RecordMoneyGained(amount)` |

---

## Public API

```csharp
// Manual record methods (call from any system)
stats.RecordCriticalHit();
stats.RecordKill();
stats.RecordItemGathered(int count = 1);
stats.RecordMoneyGained(int amount);
```

---

## Per-Stat Events

Each stat fires a change event with the new total as its argument. Subscribe from any external system — no modification to `CharacterStatistics` needed.

```csharp
stats.OnAttacksChanged      += count => { };
stats.OnDamageDealtChanged  += total => { };
stats.OnKillsChanged        += count => { };
stats.OnCriticalHitsChanged += count => { };
stats.OnItemsGatheredChanged += count => { };
stats.OnMoneyGainedChanged  += total => { };
```

---

## CombatAttacker Events (added alongside this system)

`CombatAttacker` was extended with two output events that `CharacterStatistics` subscribes to:

| Event | Fires when |
|---|---|
| `OnAttackLanded(int damage)` | A swing successfully hits a target |
| `OnKillLanded` | The hit that just landed kills the target |

These events are also available for VFX, SFX, analytics, or any other system.

---

## Adding a New Stat

1. Add a `public int NewStat { get; private set; }` property.
2. Add a `public event Action<int> OnNewStatChanged;` event.
3. Add a `RecordNewStat()` method that increments and invokes the event.
4. Call `RecordNewStat()` from wherever that stat originates.

No other files need to change.

---

## Save / Load Integration

To persist stats, read the properties in `SaveManager.Save()` and write them back in `RestoreSceneState()`. The values are plain `int`s — add them as fields to `SaveData` following the same pattern as `playerHp`.
