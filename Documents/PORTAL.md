# Portal System

Portals are authored entirely in the Unity Inspector. There is no runtime portal
JSON database and no separate local portal implementation.

## Runtime flow

```text
PortalTrigger2D detects a traveler
  -> PortalManager reads the component's destination
  -> destination scene loads when necessary
  -> destination PortalTrigger2D is found by its stable ID
  -> traveler is placed at the destination's Exit Point
```

## PortalTrigger2D

**File:** `Assets/Scripts/Portals/PortalTrigger2D.cs`

Add `PortalTrigger2D` to a scene object with a `Collider2D`. The collider is
forced to be a trigger.

| Inspector field | Purpose |
|---|---|
| Portal Id | Globally unique, stable identifier used by portals, dialogue, and quests |
| Destination Scene | Destination scene name; leave blank for the current scene |
| Destination Portal Id | ID of the receiving `PortalTrigger2D` |
| Exit Point | Child transform containing the exact arrival position |
| Required Tag | Traveler tag, normally `Player` |
| Travel Cooldown | Prevents immediate reuse |

Portal IDs should describe their location rather than their order. Prefer
`east_marsh_south_gate` over `portal_a`.

## PortalManager

**File:** `Assets/Scripts/Portals/PortalManager.cs`

`PortalManager` is a persistent singleton that handles both same-scene and
cross-scene travel.

```csharp
PortalManager.Instance.TryUsePortal(sourcePortal, playerTransform);
PortalManager.Instance.TryUsePortal("village_north_gate", playerTransform);
PortalManager.Instance.TryTeleportToPortal(
    "east_marsh_south_gate",
    playerTransform,
    "EastMarsh");
```

`TryUsePortal` follows the route stored on the source portal.
`TryTeleportToPortal` sends the traveler directly to a destination portal and
is used by NPC dialogue and scripted travel.

## Creating a same-scene pair

1. Create two portal GameObjects with trigger colliders.
2. Add `PortalTrigger2D` to each.
3. Give both globally unique IDs.
4. Leave **Destination Scene** blank.
5. Set each portal's **Destination Portal Id** to the other portal.
6. Add an `ExitPoint` child to each and assign it to **Exit Point**.
7. Position each exit point outside its portal collider.

## Creating a cross-scene pair

1. Add both scenes to Build Settings.
2. Create and configure a portal in each scene.
3. Give both portals globally unique IDs.
4. Enter the other scene's name in **Destination Scene**.
5. Enter the receiving portal's ID in **Destination Portal Id**.
6. Assign an `ExitPoint` on both portals.

Cross-scene object references are not required. The scene name and stable portal
ID are resolved after the destination scene loads.

## Dialogue travel

Dialogue choices may send the player directly to a portal:

```json
{
  "text": "Take me there.",
  "endConversation": true,
  "teleportScene": "",
  "teleportPortalId": "portal_a_2"
}
```

Leave `teleportScene` blank when the destination portal is in the current scene.

## Portal map export

Use:

```text
Tools > Portals > Export Portal Map
```

The exporter scans every Unity scene under `Assets/Scenes`, including scenes
that have not been added to Build Settings yet.

It generates:

- `Documents/Generated/portal-map.json`
- `Documents/Generated/portal-map.md`

These files are read-only documentation. Runtime gameplay never reads them.
The export also reports:

- Empty portal IDs
- Duplicate portal IDs
- Missing exit points
- Empty destination IDs
- Routes whose destination portal cannot be found

Re-run the export whenever you want a current text or JSON view of the network.
