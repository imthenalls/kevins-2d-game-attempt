# Script Function Reference

Current scripts in this project and their behavior.

---

## PlayerController2D.cs

### Awake()
**Lifecycle Event** - Called once when the script instance is loaded.

**Purpose**: Cache `Rigidbody2D` and apply physics safety settings for top-down movement.

**Details**:
- Gets `Rigidbody2D` from the same GameObject
- Sets `gravityScale` to `0` when `forceNoGravity` is enabled
- Freezes rotation when `lockRotation` is enabled

---

### Update()
**Lifecycle Event** - Called once per frame.

**Purpose**: Read movement input and prepare a normalized motion vector.

**Details**:
- Uses Input System (`Keyboard` + optional `Gamepad`) when available
- Falls back to legacy input axes when Input System is disabled
- Normalizes/clamps input to avoid faster diagonal movement

---

### FixedUpdate()
**Lifecycle Event** - Called on the physics step.

**Purpose**: Apply movement to the rigidbody.

**Details**:
- Sets `rb.linearVelocity = moveInput * moveSpeed`
