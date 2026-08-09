# Combat System

## Overview

The combat system adds hit-taking and attack logic on top of `EntityStats`. HP and MP flow is handled entirely by `EntityStats`; the combat layer adds the _interaction_ between entities — who hit whom, how hard, and what happens on death.

Player and NPC attacks include `EntityStats.BonusAttack`, allowing equipped weapons to increase final hit damage.

Attack types, elements, and status effects are not implemented yet. `DamageInfo` has a dedicated extension point for them — see **Extending the System** at the bottom.

---

## Files

| File | Description |
|---|---|
| `Assets/Scripts/Entity/DamageInfo.cs` | Struct describing one hit (amount + source) |
| `Assets/Scripts/Entity/CombatReceiver.cs` | Hit-receiving component for any entity; wraps `EntityStats` |
| `Assets/Scripts/Entity/CombatAttacker.cs` | Melee attack — shared by player and NPCs |

---

## DamageInfo

**File:** `Assets/Scripts/Entity/DamageInfo.cs`

Plain struct — no MonoBehaviour. Passed into `CombatReceiver.ReceiveHit()`.

| Field | Type | Description |
|---|---|---|
| `Amount` | `int` | Raw damage to apply |
| `Source` | `GameObject` | Who dealt the hit (may be null for traps / environment) |

```csharp
// Create and send a hit
receiver.ReceiveHit(new DamageInfo(25, gameObject));
```

---

## CombatReceiver

**File:** `Assets/Scripts/Entity/CombatReceiver.cs`

Add to any entity (player or enemy) that should participate in combat.
`RequireComponent` automatically adds `EntityStats` if it isn't already present.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Combat Enabled | true | When false, `ReceiveHit` is a no-op — the entity cannot take damage |
| Invincible | false | When true, the entity remains combat-enabled but incoming hits deal no damage |

### Runtime toggle

```csharp
receiver.CombatEnabled = false; // ignore incoming hits
receiver.CombatEnabled = true;  // re-enable incoming hits
receiver.Invincible = true;     // remain in combat but take no damage
```

### Events

```csharp
event Action<DamageInfo, EntityStats> OnHit    // fires on every landed hit
event Action<CombatReceiver>           OnDeath  // fires once when HP reaches 0
```

### API

```csharp
receiver.ReceiveHit(new DamageInfo(25, attackerGameObject));

bool alive = receiver.Stats.IsAlive;
int  hp    = receiver.Stats.Hp;
```

### Enemy death and quests

When an entity that has both `CombatReceiver` and `NpcController` (with `NpcType.Enemy`) dies, this component automatically raises:

```csharp
QuestEventBus.Raise("EnemyKilled", npcController.NpcId);
```

This satisfies any quest objective with `"eventType": "EnemyKilled"` and the matching `targetId`.

---

## CombatAttacker

**File:** `Assets/Scripts/Entity/CombatAttacker.cs`

Shared melee attack component used by both the player and NPCs.
The only difference between the two is the **Use Player Input** toggle.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Attack Damage | 10 | Damage dealt per hit |
| Attack Range | 1.5 | World-space radius scanned for target colliders |
| Attack Cooldown | 0.5 | Seconds between attacks |
| Attack Windup | 0.15 | Delay from swing start until the hit scan |
| Attack Duration | 0.3 | Duration exposed to weapon swing visuals |
| Target Layers | DefaultRaycastLayers | Layer mask this entity is allowed to hit |
| Use Player Input | true | **Player:** on. **NPC:** off — AI calls `TryAttack()` directly |
| Legacy Attack Key | Space | Fallback input when Input System is off (player only) |

### Input (player)

| Action | New Input System | Legacy |
|---|---|---|
| Attack | Space / Gamepad West (X / Square) | `legacyAttackKey` |

### How it works

1. When **Use Player Input** is on, `Update` reads input and calls `TryAttack()`.
2. The cooldown begins and `OnAttackStarted` triggers the equipped weapon animation.
3. After **Attack Windup**, `OverlapCircleNonAlloc` scans `attackRange`.
4. The nearest living `CombatReceiver` is found (always skips self).
5. `nearest.ReceiveHit(new DamageInfo(attackDamage, gameObject))` is called.
6. A cooldown timer blocks further attacks until both cooldown and swing are complete.

The swing begins even if no target is nearby; a miss simply finds no receiver at impact.
`NewScene` uses a 0.15-second windup and 0.3-second duration, placing damage at the middle
of the visible sword arc. See [WEAPON_SWING.md](WEAPON_SWING.md).

For **NPC attackers**, `NpcProximityMeleeController` calls `TryAttack()` while the player is
inside `AttackRange`. The component's cooldown accepts only valid attack starts.

A red wire circle gizmo shows the attack range in Scene view when the GameObject is selected.

---

## Scene Setup

### Player GameObject

```
Player (GameObject)
  ├── EntityStats      — HP/MP (already present)
  ├── CombatReceiver   — add if the player can also take hits from enemies
  └── CombatAttacker   — Use Player Input: ON  |  Target Layers: Enemy
```

### Enemy NPC GameObject

```
Enemy NPC (GameObject)
  ├── NpcController    — set NpcType = Enemy (auto-adds EntityStats and CombatReceiver)
  ├── CombatAttacker   — Use Player Input: OFF  |  Target Layers: Player
  └── Collider2D       — must be on the Enemy layer for the player's CombatAttacker to detect it
```

`NpcController.Awake` adds both `EntityStats` and `CombatReceiver` automatically when `NpcType = Enemy`. Do not add them manually — access them via `npcController.Stats` and `npcController.CombatReceiver`.

### Layer setup

1. Open **Edit → Project Settings → Tags and Layers**.
2. Create an **"Enemy"** layer and a **"Player"** layer.
3. Set the Player GameObject to the **Player** layer; all enemy GameObjects to the **Enemy** layer.
4. On the player's `CombatAttacker`, set **Target Layers** to **Enemy**.
5. On each enemy's `CombatAttacker`, set **Target Layers** to **Player**.

---

## Extending the System

| Goal | What to change |
|---|---|
| **Attack types / elements** | Add an `AttackType` enum field to `DamageInfo`; read it in `CombatReceiver.ReceiveHit` for resistance/weakness logic |
| **Defence / armor** | Add a `defense` field to `CombatReceiver`; subtract it from `info.Amount` before calling `TakeDamage` |
| **Hit all targets in range** | In `CombatAttacker.TryAttack`, loop all found receivers instead of picking nearest |
| **Ranged attacks / projectiles** | Create `Projectile.cs`; carry a `DamageInfo`; call `ReceiveHit` on `OnTriggerEnter2D` |
| **Enemy attacks player** | Add `NpcProximityMeleeController`; see [NPC_MELEE_AI.md](NPC_MELEE_AI.md) |
| **On-hit VFX / SFX** | Subscribe to `CombatReceiver.OnHit` and spawn a particle or play a clip |
