# Player Avatars

## Overview

World A and World B use separate playable avatar configurations while sharing one persistent
player identity. Each avatar may have different movement, dash rules, visuals, colliders,
combat components, and ability IDs. HP, maximum HP, and Wallet mana transfer between avatars.
Inventories remain separated by world as described in `TWO_WORLD_SYSTEM.md`.

## Included profiles

| Asset | Starting behavior |
|---|---|
| `Assets/Settings/WorldAPlayerProfile.asset` | Speed 6; 5-length dash at 6x; 3 charges; 15-second recharge |
| `Assets/Settings/WorldBPlayerProfile.asset` | Speed 8; 8-length dash at 8x; 2 charges; 5-second recharge |

These are starting values intended for playtesting. Editing a profile changes every avatar
that references it without copying values between scenes or prefabs.

## Unity setup

1. Put `PlayerController2D`, `EntityStats`, a `Rigidbody2D`, and a `Collider2D` on each player root.
2. Add `WorldCharacter` to each root.
3. Assign the appropriate `PlayerAvatarProfile` to **Profile**.
4. Keep each avatar's visuals, camera, and avatar-only components under its root.
5. Tag both roots `Player` so portals can locate the active traveler.

`NewScene` is wired to `WorldAPlayerProfile`; `WorldB` is wired to
`WorldBPlayerProfile`. Reusable copies are generated at:

- `Assets/Prefabs/WorldAPlayer.prefab`
- `Assets/Prefabs/WorldBPlayer.prefab`

Use **Tools > Worlds > Rebuild Player Avatar Prefabs** after deliberately changing a scene
player hierarchy that should become the new reusable template.

## Runtime behavior

`WorldCharacter` applies its profile to `PlayerController2D` during startup. During a portal
transition, `WorldTravelState` captures the outgoing avatar's shared HP and mana, activates the
incoming avatar, applies its movement profile, and restores the shared values. Each avatar keeps
its own collider, visuals, combat components, and movement tuning.

The profile also supplies starting ability IDs. Ability ownership belongs to a particular world
and is saved independently. Other mechanics can use:

```csharp
bool canPhase = WorldTravelState.Instance.HasAbility(WorldLayer.WorldB, "phase_dash");
WorldTravelState.Instance.UnlockAbility(WorldLayer.WorldB, "wall_phase");

// Components already attached to an avatar can use the shorter form:
GetComponent<WorldCharacter>().HasAbility("wall_phase");
```

Ability IDs are deliberately generic strings so later traversal, combat, interaction, or puzzle
components can use the same progression store without changing the avatar system.

## Shared and separate state

| Shared between avatars | Separate by world/avatar |
|---|---|
| Current and maximum HP | Movement speed and dash tuning |
| Mana balance, capacity, and history | Inventory contents |
| Story facts, quests, and keys | Ability unlock IDs |
| Remembered progression | Scene position and avatar components |
