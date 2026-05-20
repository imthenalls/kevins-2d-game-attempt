# Portal System

## Overview

The portal system moves any tagged GameObject (default: `Player`) from one location to another — either within the same scene or across scenes. Two independent modes cover different authoring needs.

| Mode | When to use |
|---|---|
| **Local Mode** | Simple same-scene portals; wire two `PortalTrigger2D` directly in the Inspector. No JSON required. |
| **Manager Mode** | Cross-scene portals, or same-scene portals you want to author outside Unity. All data lives in `StreamingAssets/portals.json`. |

---

## PortalTrigger2D

**File:** `Assets/Scripts/Portals/PortalTrigger2D.cs`

Place on any GameObject that should act as a portal entrance. Requires a `Collider2D` set as a trigger.

### Inspector fields

| Field | Description |
|---|---|
| Use Manager Config | Enable Manager Mode — reads from `PortalManager` using `Portal Id` |
| Portal Id | ID to look up in `portals.json` (Manager Mode only) |
| Destination Portal | The exit `PortalTrigger2D` (Local Mode only) |
| Destination Spawn Point | Optional exact `Transform` to teleport to (Local Mode) |
| Spawn Outside Destination | Calculate exit position offset from destination portal |
| Exit Side | `Above`, `Below`, `Left`, `Right` — direction to offset from destination |
| Exit Distance | World units of offset from destination portal (auto-calculated if `Auto Separation Distance` is on) |
| Required Tag | Only triggers for GameObjects with this tag (default: `Player`) |
| Travel Cooldown | Seconds before this portal can be used again (prevents bounce-back) |
| Reset Velocity | Zero out the traveler's `Rigidbody2D` velocity on arrival |
| Exit Velocity | Velocity to apply on arrival (used when `Reset Velocity` is on) |

### Local Mode wiring

1. Place Portal A and Portal B in the scene.
2. On Portal A: set `Destination Portal = Portal B`, choose `Exit Side`.
3. On Portal B: set `Destination Portal = Portal A`, choose `Exit Side`.
4. Both portals automatically block for `Travel Cooldown` after a teleport to prevent instant bounce-back.

---

## PortalManager

**File:** `Assets/Scripts/Portals/PortalManager.cs`

Singleton (`DontDestroyOnLoad`). Loads portal definitions from JSON, handles cross-scene teleportation by storing the pending destination and spawning the traveler after `SceneManager.sceneLoaded`.

### Config loading priority

1. `portalConfigJson` — a `TextAsset` assigned directly in the Inspector.
2. `Resources.Load` from `resourcesConfigPath` (default: `"Portals/portals"`).
3. `StreamingAssets/portals.json`.

### Inspector fields

| Field | Default | Description |
|---|---|---|
| Portal Config Json | — | TextAsset override (skips file loading) |
| Resources Config Path | `Portals/portals` | Path under `Resources/` |
| Streaming Assets File Name | `portals.json` | File under `StreamingAssets/` |
| Default Traveler Tag | `Player` | Tag filter for teleportation |
| Traveler Cooldown Seconds | 0.2 | Per-traveler cooldown (by instance ID) |
| Apply Destination Rotation | false | Rotate traveler on arrival |
| Reset Velocity On Teleport | true | Zero `Rigidbody2D` velocity on arrival |

### Usage from code

```csharp
// Manager Mode teleport (also triggered automatically by PortalTrigger2D)
PortalManager.Instance.TryUsePortal("town_entrance", playerTransform);
```

---

## PortalSpawnPoint

**File:** `Assets/Scripts/Portals/PortalSpawnPoint.cs`

Marks a named position in a scene. `PortalManager` looks these up by `spawnId` after a scene load to place the traveler.

Assign a unique `Spawn Id` in the Inspector to each `PortalSpawnPoint` in your scene.

---

## portals.json Schema

**Location:** `StreamingAssets/portals.json`

```json
{
  "version": 1,
  "portals": [
    {
      "id": "cave_entrance",
      "destination": {
        "scene": "Cave",
        "useSpawnPoint": true,
        "spawnId": "cave_from_overworld",
        "usePortalExitOffset": false,
        "destinationPortalId": "",
        "exitSide": "Right",
        "exitDistance": 1.25,
        "position": { "x": 0, "y": 0, "z": 0 },
        "rotationEuler": { "x": 0, "y": 0, "z": 0 }
      },
      "metadata": []
    }
  ]
}
```

### destination fields

| Field | Description |
|---|---|
| `scene` | Build Settings scene name to load. Empty = same scene. |
| `useSpawnPoint` | If true, find a `PortalSpawnPoint` with matching `spawnId` in the destination scene. |
| `spawnId` | ID to match against `PortalSpawnPoint` in the destination scene. |
| `usePortalExitOffset` | Place traveler relative to the destination portal trigger instead of spawn point. |
| `destinationPortalId` | Portal ID of the exit trigger (used with `usePortalExitOffset`). |
| `exitSide` | Direction offset from exit trigger: `Above`, `Below`, `Left`, `Right`. |
| `exitDistance` | World units of offset. |
| `position` | Absolute fallback position if no spawn point or portal is found. |
| `rotationEuler` | Optional rotation applied if `applyDestinationRotation` is enabled on `PortalManager`. |

---

## Quest integration

When a player travels through a portal, fire:

```csharp
QuestEventBus.Raise("LocationReached", areaId);
```

Do this from the portal trigger or from a scene initialization script. The portal system itself does not fire quest events.

---

## Setting Up a Same-Scene Portal (Local Mode)

1. Create two GameObjects — `PortalA` and `PortalB`.
2. Add `Collider2D` (Is Trigger ✓) to each.
3. Add `PortalTrigger2D` to each. Leave `Use Manager Config` off.
4. On `PortalA`: set `Destination Portal = PortalB`, pick `Exit Side = Right`.
5. On `PortalB`: set `Destination Portal = PortalA`, pick `Exit Side = Left`.

## Setting Up a Cross-Scene Portal (Manager Mode)

1. Add a `PortalManager` GameObject to your first scene (or a persistent scene). It will `DontDestroyOnLoad`.
2. Create `StreamingAssets/portals.json` with your portal definitions.
3. In the destination scene, add a `PortalSpawnPoint` and set its `Spawn Id` to match `spawnId` in JSON.
4. Place `PortalTrigger2D` at the entrance. Enable `Use Manager Config`, set `Portal Id` to the matching JSON id.
