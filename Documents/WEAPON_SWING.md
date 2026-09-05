# Equipped Weapon Swing

## Overview

The equipped weapon swings as part of the existing melee attack. `CombatAttacker` owns
input, cooldown, and damage timing. `EquippedWeaponVisual` listens for attack starts and
rotates `WeaponVisual` through a configurable arc around a normalized grip point.

Damage is delayed until the strike point instead of being applied when the button is first
pressed. A swing still animates when no target is in range.

## NewScene Setup

```
Player
  CombatAttacker
  EquipmentManager
  PlayerVisual
    WeaponVisual
      SpriteRenderer
      EquippedWeaponVisual
```

### Player / CombatAttacker

The component is already attached in `NewScene` with:

| Field | Value | Purpose |
|---|---:|---|
| Attack Damage | 10 | Base damage before equipped bonuses |
| Attack Range | 1.5 | Radius used for the impact scan |
| Attack Cooldown | 0.5 s | Minimum time between attacks |
| Attack Windup | 0.15 s | Delay from input to damage |
| Attack Duration | 0.3 s | Visual swing duration |
| Use Player Input | enabled | Space/gamepad West starts attacks |

The Sword Guard uses the same timings with player input disabled; its proximity AI calls
`TryAttack()` when the player is in range.

### WeaponVisual / EquippedWeaponVisual

Assign these Inspector references:

- **Equipment Manager:** Player's `EquipmentManager`.
- **Combat Attacker:** Player's `CombatAttacker`.
- **Weapon Renderer:** `WeaponVisual`'s `SpriteRenderer`.
- **Swing Transform:** the `WeaponVisual` Transform.
- **Start Angle Offset:** `-70` degrees.
- **End Angle Offset:** `70` degrees.
- **Grip Pivot Normalized:** hand position inside the sprite rect, measured from its
  bottom-left corner. The iron sword uses `(0.16, 0.18)`.

The Transform's normal local position and rotation are captured on `Awake` and restored
after every swing. Position is compensated while rotating so the grip remains stationary
even though the imported sprite itself has a centered pivot. The arc is applied as an
offset, so the held sword's authored resting pose remains editable in the scene.

The component auto-detects a parent body `SpriteRenderer`. When that renderer's **Flip X**
changes, the weapon mirrors its resting position, sprite, grip point, and swing direction.
Combat AI also applies this facing immediately before requesting an attack.

## Runtime Sequence

1. Space or gamepad West calls `CombatAttacker.TryAttack()`.
2. The cooldown starts and `OnAttackStarted` fires.
3. `EquippedWeaponVisual` eases through rest → backswing → strike → rest over 0.3 seconds,
   rotating around the grip without snapping between poses.
4. At 0.15 seconds, `CombatAttacker` scans for the nearest living `CombatReceiver`.
5. A found target receives base damage plus `EntityStats.BonusAttack`.
6. The sword returns to its stored rest rotation.

## Runtime API

```csharp
combatAttacker.TryAttack();       // starts animation, cooldown, and delayed impact
weaponVisual.PlaySwing();         // visual-only swing; does not cause another hit
weaponVisual.SetFacingLeft(true); // mirror held pose and swing to the left

float duration = combatAttacker.AttackDuration;
float impactAt = combatAttacker.AttackWindup;
```

## Tuning

Keep **Attack Windup** between zero and **Attack Duration**. For impact at the center of the
arc, set it to half the duration. Cooldown is automatically clamped to at least the visual
duration so a second swing cannot interrupt the first.
