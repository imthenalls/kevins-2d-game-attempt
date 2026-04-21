# AGENT.md

## Game Basics

This project is a **2D top-down** game.

### Core Rules
1. Perspective: **Top-down** (player moves on X/Y plane)
2. Dimension: **2D**

### Player Setup (Top-Down)
- Add `Rigidbody2D` to Player
  - Body Type: Dynamic
  - Gravity Scale: 0
  - Freeze Rotation Z: enabled
- Add a collider (`BoxCollider2D` or `CapsuleCollider2D`)
- Add [Game Scripts/PlayerController2D.cs](Game%20Scripts/PlayerController2D.cs)

### Input
- Movement: `WASD` or Arrow Keys
- Uses Unity axes:
  - `Horizontal`
  - `Vertical`

### Scene Setup
- World objects should use `Collider2D` components.
- If objects need physics movement, add `Rigidbody2D`.
- Camera is usually orthographic for top-down 2D.

### Notes
- No jump logic is needed for this controller.
- Movement is normalized so diagonal speed is not faster than straight movement.
