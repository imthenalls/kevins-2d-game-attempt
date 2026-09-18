# NPC Health Bars

## Overview

Every `NpcController` configured as `NpcType.Enemy` can display a screen-space HP bar above
its world position. The bar shows a colored fill and exact `current / maximum` HP text. It
reads the enemy's existing `EntityStats`, so damage, save loading, and runtime HP changes are
reflected automatically.

## Implementation

The bar renders with UGUI, not immediate-mode GUI (`OnGUI` was removed 2026-09 for performance —
IMGUI runs several native-to-managed callbacks per enemy per frame, while one `Update` suffices).

- `NpcController.Start` creates one `EnemyHealthBarUI` per enemy when **Show Enemy Health Bar**
  is enabled. The component lives at
  `Assets/Scripts/GamePresentation/NPCs/EnemyHealthBarUI.cs`.
- Bars are parented to a shared screen-space overlay canvas ("Enemy Health Bars") that is created
  on demand and uses sorting order 900, below the dialogue UI (1000).
- Each frame the bar repositions from `Camera.main.WorldToScreenPoint` and refreshes fill width,
  fill color (red→green lerp by HP ratio), and the `current / maximum` label. The same hide rules
  as before apply: feature off, non-enemy, no stats, dead enemy, missing camera, or a behind-camera
  position all hide the bar visuals.
- The bar destroys itself when its owner is gone, so scene changes leave no orphaned bars.

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

At zero HP, `CombatReceiver` disables the enemy's behavior and stops its Rigidbody2D. The bar
hides itself at that moment (the bar follows the enemy only while it is alive).

## Inspector Fields

| Field | Default | Purpose |
|---|---:|---|
| Show Enemy Health Bar | true | Enables the bar for this enemy |
| Health Bar World Offset | 0.8 | Vertical world-space offset above the NPC |
| Health Bar Screen Size | 72 × 12 | Bar dimensions in screen pixels |
