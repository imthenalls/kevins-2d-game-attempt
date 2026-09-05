# NPC Health Bars

## Overview

Every `NpcController` configured as `NpcType.Enemy` can display a screen-space HP bar above
its world position. The bar shows a colored fill and exact `current / maximum` HP text. It
reads the enemy's existing `EntityStats`, so damage, save loading, and runtime HP changes are
reflected automatically.

## Unity Setup

No additional component or Canvas is required.

1. Add `NpcController` to the NPC GameObject.
2. Set **Npc Type** to **Enemy**. This creates/configures `EntityStats` and
   `CombatReceiver` during `Awake`.
3. Keep **Show Enemy Health Bar** enabled.
4. Adjust **Health Bar World Offset** to place the bar above the sprite.
5. Adjust **Health Bar Screen Size** to change its pixel dimensions.
6. Ensure the gameplay camera is tagged **MainCamera**.

The bar is not shown for friendly, vendor, trainer, or quest-giver NPCs.

## Combat Setup

The player requires `CombatAttacker` with **Use Player Input** enabled. Enemy NPCs require a
`Collider2D`; `NpcController` supplies their `CombatReceiver` at runtime. Player invincibility
does not prevent outgoing attacks—it only makes the player's `CombatReceiver` ignore incoming
damage.

In `NewScene`, the player is combat-enabled and remains invincible. A player holding the Iron
Sword deals 20 damage (10 base plus 10 equipment bonus), while each Sword Guard has 30 HP.
Their displayed HP therefore changes from `30 / 30` to `10 / 30`, then `0 / 30` after two
connected swings.

At zero HP, `CombatReceiver` disables the enemy's behavior and stops its Rigidbody2D. The
empty bar remains visible while the object is available for loot or later death presentation.

## Inspector Fields

| Field | Default | Purpose |
|---|---:|---|
| Show Enemy Health Bar | true | Enables the bar for this enemy |
| Health Bar World Offset | 0.8 | Vertical world-space offset above the NPC |
| Health Bar Screen Size | 72 × 12 | Bar dimensions in screen pixels |
