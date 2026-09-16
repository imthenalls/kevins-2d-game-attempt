# Equipped Weapon Swing

> **Game.Data config:** swing arc, after-swing trail, and grip pivot tuning live in the pure-C#
> `Game.Core.WeaponVisualConfig` (`Game.Data` assembly) and appear under **Settings** in the
> Inspector. `EquippedWeaponVisual` keeps only its Unity references (`Transform`, `SpriteRenderer`).

## Overview

The equipped weapon swings as part of the existing melee attack. `CombatAttacker` owns
input, cooldown, and damage timing. `EquippedWeaponVisual` listens for attack starts and
rotates `WeaponVisual` through a one-way configurable arc around a normalized grip point.

Damage is driven by actual weapon contact. A runtime blade-shaped trigger follows the visible
sword and can damage only hurtbox colliders it overlaps during the forward swing. A player
cannot begin or buffer an attack while the Weapon equipment slot is empty.

## NewScene Setup

```
Player
  CombatAttacker
  EquipmentManager
  PlayerVisual
    WeaponVisual
      SpriteRenderer
      EquippedWeaponVisual
      PolygonCollider2D (created automatically at runtime)
```

### Player / CombatAttacker

The component is already attached in `NewScene` with:

| Field | Value | Purpose |
|---|---:|---|
| Attack Damage | 10 | Base damage before equipped bonuses |
| Attack Range | 1.5 | NPC AI distance for deciding when to begin a swing |
| Attack Cooldown | 0.5 s | Minimum time between attacks |
| Attack Windup | 0.15 s | Legacy compatibility value; no longer performs a radius hit scan |
| Attack Duration | 0.3 s | Visual swing and blade-contact window duration |
| Attack Buffer Window | 0.5 | A second press after 50% progress queues the next swing |
| Use Player Input | enabled | Space/gamepad West starts attacks |

The Sword Guard uses the same timings with player input disabled; its proximity AI calls
`TryAttack()` when the player is in range.

### WeaponVisual / EquippedWeaponVisual

Assign these Inspector references:

- **Equipment Manager:** Player's `EquipmentManager`.
- **Combat Attacker:** Player's `CombatAttacker`.
- **Weapon Renderer:** `WeaponVisual`'s `SpriteRenderer`.
- **Swing Transform:** the `WeaponVisual` Transform.
- **Start Angle Offset:** `-20` degrees (lower-right attack-start position).
- **End Angle Offset:** `200` degrees (lower-left attack-end position).
- **Attack Radius Multiplier:** `1.25` (the sweep runs outside the neutral holding radius).
- **Trail Color:** bright translucent red.
- **Trail Fade Time:** `0.28` seconds.
- **Grip Pivot Normalized:** hand position inside the sprite rect, measured from its
  bottom-left corner. The iron sword uses `(0.16, 0.18)`.

The Transform's normal local position and rotation are captured on `Awake` as the neutral
holding pose. The attack pose is separate: when a swing begins, the sword immediately jumps
to the configured start angle around the character. It then sweeps to the end angle and
returns directly to the saved holding pose only after the animation finishes.

Position is compensated around the imported sprite's normalized grip point. The grip also
orbits around the owning character's local origin, moving the entire sword in a broad fan.
With `-20` to `200`, the grip starts at the lower-right, passes over the character, and ends
at the lower-left in a 220-degree forward sweep. The separate attack-radius multiplier keeps
this path outside the neutral holding position, so crossing the same angle as the held sword
cannot look like an early reset.

During the forward swing, a runtime `TrailRenderer` child follows the blade tip. Its leading
edge beside the sword is the thickest and most opaque part. Older points taper down and
become transparent farther behind the sword. Emission stops at the end of the attack, but
the completed red arc remains briefly and fades away while the sword returns to neutral.

`EquippedWeaponVisual` also creates a trigger `PolygonCollider2D` directly on WeaponVisual.
Its four points form a narrow blade from just above the configured grip to the sprite's tip.
It mirrors with the renderer, enables only during a real attack, and checks overlaps after
each rendered swing update. Existing entity colliders beneath a `CombatReceiver` are the
hurtboxes; no separate hurtbox script is required. Each receiver can take damage once per
swing, even if it remains inside the blade for several frames.

The component auto-detects a parent body `SpriteRenderer`. When that renderer's **Flip X**
changes, the weapon mirrors its resting position, sprite, grip point, and swing direction.
Combat AI also applies this facing immediately before requesting an attack. Once a swing
starts, its facing is locked until the complete arc finishes. Any movement or AI facing
change requested during that time is queued and applied after the neutral reset, preventing
the facing synchronizer from cancelling the animation partway through.

## Runtime Sequence

1. Space or gamepad West calls `CombatAttacker.TryAttack()` only if the Weapon equipment
   slot contains an item.
2. The cooldown starts and `OnAttackStarted` fires.
3. `EquippedWeaponVisual` jumps from the neutral holding pose to the lower-right attack pose.
4. It eases forward over the character to the lower-left attack pose over 0.3 seconds.
5. The runtime blade polygon checks which `CombatReceiver` hurtboxes it physically overlaps.
6. Every valid touched target receives base damage plus `EntityStats.BonusAttack`, once.
7. When the forward animation ends, the sword is restored directly to its stored neutral
   position. There is no animated reverse or recovery swing.

## Late-swing attack buffering

While player input is enabled, one additional attack can be queued during the final 50% of
the current animation. Earlier presses are ignored. If a valid late press is received, the
queued attack starts immediately when the current visual swing ends, even if the ordinary
cooldown still has time remaining. Holding the button does not queue an attack; it requires
a new press, and only one follow-up can be stored at a time.

## Runtime API

```csharp
combatAttacker.TryAttack();       // player starts only with a weapon equipped
combatAttacker.TryApplyWeaponHit(receiver); // called by the runtime blade hitbox
weaponVisual.PlaySwing();         // visual-only swing; does not cause another hit
weaponVisual.SetFacingLeft(true); // mirror held pose and swing to the left

float duration = combatAttacker.AttackDuration;
bool canContactDamage = combatAttacker.IsWeaponHitWindowOpen;
```

## Tuning

The blade collider width is derived from the current sprite size, while its base uses **Grip
Pivot Normalized**. Adjust that grip first if the hitbox appears offset from a new weapon
sprite. Cooldown is automatically clamped to at least the visual duration for ordinary
attacks; a correctly timed buffered follow-up can chain at the animation boundary.
