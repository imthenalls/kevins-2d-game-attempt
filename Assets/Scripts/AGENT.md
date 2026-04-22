# 2D Top-Down Game Basics (Unity)

This game setup is:
1. **2D**
2. **Top-down** (no gravity)

## Core idea
You move the player on the X/Y plane using keyboard input.
Gravity is disabled so the character does not fall.

## Player GameObject requirements
- `SpriteRenderer`
- `Rigidbody2D`
- `Collider2D` (Box/Circle/Capsule)
- `PlayerController2D` script

## Rigidbody2D settings for top-down
- Body Type: `Dynamic`
- Gravity Scale: `0`
- Freeze Rotation Z: enabled (or done by script)

## How movement works
- Read `Horizontal` + `Vertical` input in `Update()`
- Build a 2D direction vector
- Normalize it (so diagonal movement is not faster)
- Apply velocity in `FixedUpdate()`

Formula:
- `velocity = normalizedInput * moveSpeed`

## Scene checklist
- Main Camera set to **Orthographic**
- Player has Rigidbody2D + Collider2D + script
- Walls/obstacles have Collider2D
- Optional: add colliders to map boundaries

## Common issues
- Not moving: script not attached, missing Rigidbody2D, or speed is 0
- Falling: gravity scale is not 0
- Spinning: rotation not frozen
- Fast diagonal movement: input not normalized

## Good next steps
- Add idle/walk animation
- Add interaction key (`E`)
- Add enemies and simple AI
- Add health + UI

