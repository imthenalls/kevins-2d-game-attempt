# Combat System

## Overview

The combat system adds hit-taking and attack logic on top of `EntityStats`. HP and MP flow is handled entirely by `EntityStats`; the combat layer adds the _interaction_ between entities — who hit whom, how hard, and what happens on death.

Attack types, elements, and status effects are not implemented yet. `DamageInfo` has a dedicated extension point for them — see **Extending the System** at the bottom.

---

## Files

| File | Description |
|---|---|
| `Assets/Scripts/Combat/DamageInfo.cs` | Struct describing one hit (amount + source) |
| `Assets/Scripts/Combat/Combatant.cs` | Combat component for any entity; wraps EntityStats |
| `Assets/Scripts/Combat/PlayerCombat.cs` | Player attack input and nearest-enemy hit detection |

---

## DamageInfo

**File:** `Assets/Scripts/Combat/DamageInfo.cs`

Plain struct — no MonoBehaviour. Passed into `Combatant.ReceiveHit()`.

| Field | Type | Description |
|---|---|---|
| `Amount` | `int` | Raw damage to apply |
| `Source` | `GameObject` | Who dealt the hit (may be null for traps / environment) |

```csharp
// Create and send a hit
combatant.ReceiveHit(new DamageInfo(25, gameObject));
```

---

## Combatant

**File:** `Assets/Scripts/Combat/Combatant.cs`

Add to any entity (player or enemy) that should participate in combat.
`RequireComponent` automatically adds `EntityStats` if it isn't already present.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Combat Enabled | true | When false, `ReceiveHit` is a no-op — the entity cannot take damage |

### Runtime toggle

```csharp
combatant.CombatEnabled = false; // invincible (cutscene, spawn grace period, etc.)
combatant.CombatEnabled = true;  // re-enable
```

### Events

```csharp
event Action<DamageInfo, EntityStats> OnHit    // fires on every landed hit
event Action<Combatant>               OnDeath  // fires once when HP reaches 0
```

### API

```csharp
combatant.ReceiveHit(new DamageInfo(25, attackerGameObject));

bool alive = combatant.Stats.IsAlive;
int  hp    = combatant.Stats.Hp;
```

### Enemy death and quests

When an entity that has both `Combatant` and `NpcController` (with `NpcType.Enemy`) dies, this component automatically raises:

```csharp
QuestEventBus.Raise("EnemyKilled", npcController.NpcId);
```

This satisfies any quest objective with `"eventType": "EnemyKilled"` and the matching `targetId`.

---

## PlayerCombat

**File:** `Assets/Scripts/Combat/PlayerCombat.cs`

Attach to the **Player** GameObject alongside `PlayerController2D`.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Attack Damage | 10 | Damage dealt per hit |
| Attack Range | 1.5 | World-space radius scanned for enemy colliders |
| Attack Cooldown | 0.5 | Seconds between attacks |
| Enemy Layers | DefaultRaycastLayers | Layer mask for enemy colliders |
| Legacy Attack Key | Space | Fallback when Input System is off |

### Input

| Action | New Input System | Legacy |
|---|---|---|
| Attack | Space / Gamepad West (X / Square) | `legacyAttackKey` |

### How it works

1. Player presses the attack key.
2. `OverlapCircleNonAlloc` scans `attackRange` for colliders on `enemyLayers`.
3. The nearest living `Combatant` is found (skips self).
4. `nearest.ReceiveHit(new DamageInfo(attackDamage, gameObject))` is called.
5. A cooldown timer blocks further attacks until it expires.

A red wire circle gizmo shows the attack range in Scene view when the Player is selected.

---

## Scene Setup

### Player GameObject

```
Player (GameObject)
  ├── EntityStats      — HP/MP (already present)
  ├── Combatant        — add if the player can also take hits from enemies
  └── PlayerCombat     — add for attack input and hit detection
```

### Enemy NPC GameObject

```
Enemy NPC (GameObject)
  ├── NpcController    — set NpcType = Enemy (auto-adds EntityStats and Combatant)
  └── Collider2D       — must be on an enemy layer for PlayerCombat to detect it
```

`NpcController.Awake` adds both `EntityStats` and `Combatant` automatically when `NpcType = Enemy`. Do not add them manually — access them via `npcController.Stats` and `npcController.Combatant`.

### Layer setup

1. Open **Edit → Project Settings → Tags and Layers**.
2. Create an **"Enemy"** layer.
3. Set all enemy GameObjects to that layer.
4. On `PlayerCombat`, set **Enemy Layers** to include the **"Enemy"** layer.

---

## Extending the System

| Goal | What to change |
|---|---|
| **Attack types / elements** | Add an `AttackType` enum field to `DamageInfo`; read it in `Combatant.ReceiveHit` for resistance/weakness logic |
| **Defence / armor** | Add a `defense` field to `Combatant`; subtract it from `info.Amount` before calling `TakeDamage` |
| **Hit all enemies in range** | In `PlayerCombat.TryAttack`, loop all found combatants instead of picking nearest |
| **Ranged attacks / projectiles** | Create `Projectile.cs`; carry a `DamageInfo`; call `ReceiveHit` on `OnTriggerEnter2D` |
| **Enemy attacks player** | Add an attack behavior script (similar to `NpcWanderBehavior`) that calls `playerCombatant.ReceiveHit(...)` on a timer |
| **On-hit VFX / SFX** | Subscribe to `Combatant.OnHit` and spawn a particle or play a clip |
