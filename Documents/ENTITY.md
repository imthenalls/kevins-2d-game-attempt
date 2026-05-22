# Entity System

## Overview

The `Entity/` folder holds the foundational scripts shared by every living thing in the game — player, NPCs, and enemies. All other systems (UI, AI, quests) build on top of these.

---

## Files

| File | Description |
|---|---|
| `Assets/Scripts/Entity/IEntityController.cs` | Shared interface — implemented by both `PlayerController2D` and `NpcController` |
| `Assets/Scripts/Entity/EntityStats.cs` | HP and MP tracking with events |
| `Assets/Scripts/Entity/Combatant.cs` | Marks an entity as hittable; wraps `EntityStats` for combat |
| `Assets/Scripts/Entity/DamageInfo.cs` | Struct describing one hit (amount + source) |
| `Assets/Scripts/Entity/CombatAttacker.cs` | Melee attack — shared by player and NPCs |

---

## IEntityController

**File:** `Assets/Scripts/Entity/IEntityController.cs`

Implemented by `PlayerController2D` and `NpcController`. Use this interface when a script needs to work with either without caring which one it is — useful for shared prefabs, combat, cutscenes, and AI.

```csharp
public interface IEntityController
{
    string     DisplayName     { get; }
    EntityStats Stats          { get; }
    Combatant   Combatant      { get; }
    bool        MovementEnabled { get; }
    void        SetMovementEnabled(bool enabled);
}
```

### Usage in a script

```csharp
// Inspector field accepts either PlayerController2D or NpcController
[SerializeField] private MonoBehaviour controllerSource;
private IEntityController _controller;

private void Awake()
{
    _controller = (IEntityController)controllerSource;
}
```

### Null safety

`Stats` and `Combatant` may be null on non-combat entities (e.g. generic NPCs with `NpcType != Enemy`). Always null-check before use:

```csharp
_controller.Stats?.TakeDamage(10);
_controller.Combatant?.ReceiveHit(new DamageInfo(10, gameObject));
```

---

## EntityStats

**File:** `Assets/Scripts/Entity/EntityStats.cs`

See [STATS.md](STATS.md) for full API documentation and UI setup.

---

## Combatant, DamageInfo, CombatAttacker

See [COMBAT.md](COMBAT.md) for full documentation, inspector fields, and scene setup.

---

## How the Pieces Fit Together

```
IEntityController
  ├── PlayerController2D  (player moves via input)
  └── NpcController       (NPC moves via AI behaviors)

EntityStats  — HP/MP — used by player and Enemy NPCs
  └── Combatant           — hit-taking layer, hooks OnDeath for quest events
        └── CombatAttacker — finds and hits nearby Combatants
```

An entity becomes a full combat participant by having all three components:
`EntityStats` + `Combatant` + `CombatAttacker`.

For **enemies**, `NpcController` (with `NpcType = Enemy`) auto-adds `EntityStats` and `Combatant`. Add `CombatAttacker` manually with **Use Player Input** disabled.

For the **player**, all three are added manually in the Inspector. `PlayerController2D` requires `EntityStats`; add `Combatant` and `CombatAttacker` alongside it.
