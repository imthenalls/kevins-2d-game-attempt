# Combat System

> **Game.Data config:** melee-attack tuning lives in the pure-C# `Game.Core.CombatAttackerConfig`
> (`Game.Data` assembly) and appears under **Settings** in the Inspector. `CombatAttacker` keeps
> only its hit-mask `LayerMask`. (`CombatReceiver` holds no tuning, only runtime flags.)

## Overview

The combat system adds hit-taking and attack logic on top of `EntityStats`. HP and MP flow is handled entirely by `EntityStats`; the combat layer adds the _interaction_ between entities — who hit whom, how hard, and what happens on death.

Player and NPC attacks include `EntityStats.BonusAttack`, allowing equipped weapons to increase final hit damage.

Attack types, elements, and status effects are not implemented yet. `DamageInfo` has a dedicated extension point for them — see **Extending the System** at the bottom.

---

## Files

| File | Description |
|---|---|
| `Assets/Scripts/GamePresentation/Entity/DamageInfo.cs` | Struct describing one hit (amount + source) |
| `Assets/Scripts/GamePresentation/Entity/CombatReceiver.cs` | Hit-receiving component for any entity; wraps `EntityStats` |
| `Assets/Scripts/GamePresentation/Entity/CombatAttacker.cs` | Melee timing, equipment gate, and contact damage — shared by player and NPCs |

---

## DamageInfo

**File:** `Assets/Scripts/GamePresentation/Entity/DamageInfo.cs`

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

**File:** `Assets/Scripts/GamePresentation/Entity/CombatReceiver.cs`

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
It also sets the defeated NPC's behavior state to `Disabled` and clears Rigidbody2D velocity,
stopping wander and melee AI while leaving the object available for loot/death presentation.

---

## CombatAttacker

**File:** `Assets/Scripts/GamePresentation/Entity/CombatAttacker.cs`

Shared melee attack component used by both the player and NPCs.
The only difference between the two is the **Use Player Input** toggle.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Attack Damage | 10 | Damage dealt per hit |
| Attack Range | 1.5 | NPC AI engagement distance; it does not deal damage by radius |
| Attack Cooldown | 0.5 | Seconds between attacks |
| Attack Windup | 0.15 | Legacy serialized timing retained for existing scenes/listeners |
| Attack Duration | 0.3 | Visual swing and weapon-contact damage-window duration |
| Attack Buffer Window | 0.5 | Final 50% of a player swing accepts one queued follow-up press |
| Target Layers | DefaultRaycastLayers | Layer mask this entity is allowed to hit |
| Use Player Input | true | **Player:** on. **NPC:** off — AI calls `TryAttack()` directly |
| Legacy Attack Key | Space | Fallback input when Input System is off (player only) |

### Input (player)

| Action | New Input System | Legacy |
|---|---|---|
| Attack | Space / Gamepad West (X / Square) | `legacyAttackKey` |

### How it works

1. When **Use Player Input** is on, `Update` reads input. If the Weapon equipment slot is
   empty, the input is rejected and no attack animation starts.
2. The cooldown and active damage window begin, and `OnAttackStarted` triggers the equipped
   weapon animation.
3. `EquippedWeaponVisual` enables a runtime `PolygonCollider2D` shaped from the sword grip
   toward its blade tip and checks its overlaps as the visible sword moves.
4. An overlapped collider is resolved to its parent `CombatReceiver`, which acts as the
   entity's hurtbox. Target-layer and self-hit rules are then applied.
5. Every valid receiver touched by the blade receives base damage plus
   `EntityStats.BonusAttack`, at most once per swing.
6. The hitbox disables when the swing ends. A cooldown timer blocks ordinary repeated
   attacks. For player-controlled attackers, one
   new press during the final buffer window queues a follow-up that begins when the current
   animation ends.

An equipped weapon still swings if no target is nearby, but the miss deals no damage. Merely
standing within `AttackRange` cannot be hit: the blade polygon must physically overlap the
receiver's Collider2D during the visible arc. See [WEAPON_SWING.md](WEAPON_SWING.md).

For **NPC attackers**, `NpcProximityMeleeController` calls `TryAttack()` while the player is
inside `AttackRange`. The component's cooldown accepts only valid attack starts.

A red wire circle gizmo shows the attack range in Scene view when the GameObject is selected.

---

## Scene Setup

### Player GameObject

```
Player (GameObject)
  ├── EntityStats      — HP/MP (already present)
  ├── CombatReceiver   — add if the player can also take hits from enemies; Collider2D is its hurtbox
  ├── EquipmentManager — Weapon slot must contain an item before player input can attack
  ├── CombatAttacker   — Use Player Input: ON  |  Target Layers: Enemy
  └── WeaponVisual     — EquippedWeaponVisual auto-creates the runtime blade hitbox
```

### Enemy NPC GameObject

```
Enemy NPC (GameObject)
  ├── NpcController    — set NpcType = Enemy (auto-adds EntityStats and CombatReceiver)
  ├── CombatAttacker   — Use Player Input: OFF  |  Target Layers: Player
  └── Collider2D       — enemy hurtbox; must be on a permitted target layer
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
| **Limit cleave targets** | Add a maximum target count in `CombatAttacker.TryApplyWeaponHit` |
| **Ranged attacks / projectiles** | Create `Projectile.cs`; carry a `DamageInfo`; call `ReceiveHit` on `OnTriggerEnter2D` |
| **Enemy attacks player** | Add `NpcProximityMeleeController`; see [NPC_MELEE_AI.md](NPC_MELEE_AI.md) |
| **On-hit VFX / SFX** | Subscribe to `CombatReceiver.OnHit` and spawn a particle or play a clip |
