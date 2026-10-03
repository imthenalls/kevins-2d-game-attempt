# School Underground Hallway

A locked **Industrial Arts Shop** (`wood_shop` zone) leads through a physical 3D door into an
**underground hallway** built inside the Town scene, and the far end is a one-way drop into the
**town park**. The route connects scene building, a dialogue reward, the keyring, a physical lock,
same-scene portals, and persistence.

```text
Town (World A)
  └─ Main Building Room (foyer)  ── school_gate ⇄ school_entrance ──▶  School Interior
        └─ north corridor: Workshop Keeper (key NPC)
              └─ locked Workshop Door (3D) ──▶ Industrial Arts Shop (wood_shop)
                    └─ school_workshop_portal ⇄ hall_entrance ──▶ Underground Hallway
                          └─ hall_exit ──one way──▶ park_arrival (town park, no return)
```

## Route pieces

| Piece | Id | Where | Notes |
|---|---|---|---|
| Workshop Door | (component) | `School Interior/Rooms/Workshop Door` | `LockedDoor3D`, requires `wood_shop_key`, reusable |
| Workshop Portal | `school_workshop_portal` | inside `wood_shop` | blank Destination Scene, Changes World off |
| Hall Entrance Portal | `hall_entrance` | hallway left end | returns to `school_workshop_portal` |
| Hall Exit Portal | `hall_exit` | hallway right end | one-way to `park_arrival` |
| Park Arrival | `park_arrival` | town park | arrival-only, no collider / no route |
| Workshop Keeper | `school_workshop_keeper` | north corridor beside `wood_shop` | owns exactly one `wood_shop_key` |

## The locked 3D door

`SlidingDoor` is 2D (`Collider2D`, grid cells) and is **not** used here.
`LockedDoor3D` (`Assets/Scripts/GamePresentation/World/LockedDoor3D.cs`) is the 3D adapter:

- The lock decision and door state live in Engine-Free Core: `Game.Core.DoorLockPolicy` and
  `Game.Core.LockedDoorModel`, with tuning in `Game.Core.Door3DConfig`.
- It builds (in edit and play mode, `[ExecuteAlways]`) an interaction trigger `BoxCollider` on the
  root, an invisible **DoorBlocker** `BoxCollider` child on the **Walls** layer, and two sliding
  panel sprites. The blocker is what blocks the player, pathfinding, and the reachability test.
- Key checks resolve the interacting entity's `IKeyHolder` (falling back to `PlayerKeyring` for the
  player), so the same door works for a player or an NPC.
- `RemainUnlocked` is on and `ConsumeKeyOnUnlock` is off, so the key is reusable and the door stays
  open after the first unlock.

The Industrial Arts Shop has exactly **one** entrance (a two-cell doorway to the north corridor),
which the builder verifies (`VerifyWorkshopEntrance`). The single door covers it, so there is no way
around the lock.

## The key and the branching dialogue

The `wood_shop_key` item is defined in `items.json` with `["Unique","KeyItem"]`. The keeper
(`school_workshop_keeper`) is seeded with exactly one key from `npc_inventories.json` (once, tracked
by the NPC inventory initialization state — a save/load never reseeds it).

The conversation `school_workshop_keeper` (in `dialogues.json`) branches:

- **“I'm checking the old maintenance passage.”** — the successful answer. Its choice carries
  `setWorldFlag: "school_workshop_access"`.
- “Just looking around.” and “Never mind.” — no flag, no reward.

`NpcDialogue` gates its inventory gift with the new `giftRequiredFlag` field and the Core rule
`DialogueGiftPolicy`: the gift only transfers when the successful branch set the flag. Repeating the
conversation, answering wrong, cancelling, or walking away awards nothing. Because the transfer is
atomic and the key is unique, the one key cannot be duplicated; it also routes to the keyring even
when the ordinary inventory is full.

## The underground hallway

`UndergroundHallwayBuilder` (editor-only) builds a long **inverted-U** corridor with two bends and
both ends near the bottom (low Z), spatially clear of the town rooms and the school:

- Legs at x12–14 and x28–30, z −90…−67; a top bar at z −69…−67.
- Floored on XZ, enclosed by height-1 Walls-layer collision boxes, and visible to the camera.
- Each portal sits on a light-green pad with a red centre.
- The player, World A, inventory, keyring, camera follow, and UI are preserved (same scene, no world
  change, no scene load).

## The one-way park drop

`hall_exit` targets `park_arrival`, a `PortalArrival3D`. That component implements `IPortalRoute`
(it can be found as a receiving destination by `PortalManager`) but has **no collider, no trigger,
no outgoing route**, and `IsArrivalOnly` is true. Walking over it does nothing, so the park has no
usable return portal. `GameDataValidator` registers arrival-only ids as known destinations while
still treating only real triggers as routes.

## Scene building / rebuilds

- `SchoolInteriorBuilder.BuildInto()` adds the door, workshop portal, and keeper.
- `UndergroundHallwayBuilder.BuildInto()` adds the hallway and park arrival; it is called by
  `Town3DSceneBuilder.Build()`.
- To update an already-authored scene without a full Town rebuild:
  1. **Tools > Worlds > Town 3D > Refresh School Interior**
  2. **Tools > Worlds > Town 3D > Refresh Underground Hallway**

## Verification

- `LockedDoorModelTests` / `DialogueGiftPolicyTests` (Edit Mode, engine-free) cover the lock and gift
  rules.
- `SchoolUndergroundHallwayPlayModeTests` covers same-scene portal wiring, the locked 3D doorway,
  correct versus wrong dialogue, duplicate-prevention, a full ordinary inventory, no reseed after
  consumption, travel, and the absence of a park return route.
- `SchoolPortalPlayModeTests` verifies every school room is reachable **except** the intentionally
  locked workshop, and that the workshop becomes reachable once the key is held.
- `Tools/verify-all.ps1` runs the whole suite.
