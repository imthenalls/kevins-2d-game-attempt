# Player Dash

## Overview

`PlayerController2D` includes a directional dash. Press **Left Shift** to push the player in
the direction their visual is currently facing. Movement input is ignored during the short
dash, and normal movement resumes immediately afterward.

The player begins with three dash charges. Each dash consumes one charge, and one missing
charge returns every 15 seconds until all three are restored. The default dash travels five
player lengths at six times the current movement speed. The player length is measured at
runtime from the largest dimension of the root `Collider2D`. With the current one-unit
collider and movement speed of 6, the player travels about 5 world units at 36 units per
second.

## Unity setup

No additional component is required.

1. Select the Player GameObject containing `PlayerController2D`.
2. Keep its root `Rigidbody2D` and `Collider2D` configured normally.
3. Assign **Visual Transform** so the dash can read the character's exact facing direction.
4. Configure the Dash fields:

| Field | Default | Purpose |
|---|---:|---|
| Dash Distance In Player Lengths | 5 | Target distance relative to collider size |
| Dash Speed Multiplier | 6 | Multiplier applied to current Move Speed |
| Dash Cooldown | 0.4 s | Minimum time before another dash can begin |
| Max Dash Charges | 3 | Number of stored dashes available at full charge |
| Dash Recharge Seconds | 15 s | Time required to restore each missing charge |
| Legacy Dash Key | Left Shift | Used when the new Input System is disabled |
| Dash Trail Color | Cyan-blue | Color and leading opacity of the trail |
| Dash Trail Fade Time | 0.3 s | How long the trail remains after emission stops |
| Dash Trail Width In Player Lengths | 0.8 | Width beside the player before tapering |

No Canvas, UI, prefab, or hierarchy additions are required.

## Runtime behavior

1. `Update()` detects a new Left Shift press.
2. One available dash charge is consumed and the recharge timer begins if it was full.
3. The controller calculates forward from `Visual Transform` and `Sprite Forward Angle`.
4. Dash duration is calculated as `distance / (Move Speed * multiplier)`.
5. `FixedUpdate()` applies dash velocity and scales the final physics step to avoid distance
   overshoot.
6. Dialogue, inventory, or another system calling `SetMovementEnabled(false)` immediately
   cancels an active dash.
7. One missing charge is restored every 15 seconds; spending additional charges does not
   restart the recharge already in progress.

## Dash trail

`PlayerController2D` creates a `Player Dash Trail` child at runtime. It emits only while the
dash is moving. The trail is widest and most opaque beside the player, then becomes thinner
and transparent farther behind. When the dash ends, emission stops and the existing trail
fades over 0.3 seconds. No trail object needs to be added manually in the scene.

## Camera framing

`NewScene` uses an orthographic camera size of `6.5`, increased from `5`. This is a 30%
zoom-out and shows more of the surrounding world during fast dash movement.

## Runtime API

```csharp
bool dashing = playerController.IsDashing;
int charges = playerController.CurrentDashCharges;
int maximum = playerController.MaxDashCharges;
playerController.SetMovementEnabled(false); // also cancels a dash
```
