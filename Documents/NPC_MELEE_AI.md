# NPC Melee AI

## Overview

`NpcProximityMeleeController` combines the existing wander and combat systems. While the
player is outside melee range, `NpcBehaviorManager` runs `NpcWanderBehavior`. Inside range,
the controller enters `NpcBehaviorState.Combat`, stops the Rigidbody2D, faces the player,
and repeatedly calls `CombatAttacker.TryAttack()`.

The attack component's cooldown prevents attack spam. Its `OnAttackStarted` event drives
`EquippedWeaponVisual`, so NPC and player swords use the same swing and impact timing.

## Unity Setup

Add these components to the NPC root:

1. `NpcController`, set to `NpcType.Enemy` if the player should be able to damage it.
2. `Rigidbody2D`, with Gravity Scale 0 and Freeze Rotation Z enabled.
3. A `Collider2D`.
4. `NpcBehaviorManager`.
5. `NpcWanderBehavior`, with its body SpriteRenderer assigned.
6. `CombatAttacker`, with **Use Player Input** disabled.
7. `NpcProximityMeleeController`; assign Player, NPC Controller, Rigidbody, Attacker, and
   Body Renderer, or leave them empty for automatic discovery.
8. `EquipmentManager` and a `WeaponVisual` child with `EquippedWeaponVisual` if the weapon
   should be visible.

The player requires `CombatReceiver` for NPC impacts to reduce player HP.

## Sword Guard in NewScene

```
sword guard npc
  NpcController                  Enemy, 30 HP
  Rigidbody2D                    Gravity 0, frozen rotation
  CircleCollider2D
  NpcBehaviorManager
  NpcWanderBehavior              Radius 3, speed 1.4
  NpcProximityMeleeController
  CombatAttacker                 Range 1.5, input disabled
  EquipmentManager              starts with iron_sword
  WeaponVisual
    SpriteRenderer
    EquippedWeaponVisual
```

The NPC begins at `(3, -1.5)`. The player's scene object now includes `CombatReceiver`.

## Runtime Behavior

1. Wander chooses and walks toward random nearby positions.
2. When the living player is within `CombatAttacker.AttackRange`, normal behavior pauses.
3. The NPC velocity becomes zero and the body sprite faces the player.
4. `TryAttack()` is requested each frame; its internal cooldown accepts only valid swings.
5. The sword animates immediately and damage is resolved at the configured windup time.
6. When the player leaves range, the NPC returns to `Idle` state and wandering resumes.

## Runtime API

```csharp
bool fighting = meleeController.IsEngaged;
float range = combatAttacker.AttackRange;
```
