# School Interior

The school is a large planar-isometric interior that lives **inside the existing Town scene**
(`Assets/Scenes/Town.unity`) — it is not a separate scene. It sits east of the town and its rooms,
under a single `School Interior` root, and the main-building foyer links the Town to it with a
**same-scene** portal pair (no scene load).

Route: `Town` ⇄ main-building foyer (`Main Building Room`) ⇄ `School Interior`.

## How it fits together

| Piece | Where | Purpose |
|---|---|---|
| `School Interior` root | `Town.unity`, world `(100, 0, 0)` | Container for all school geometry; moving the root moves the whole school |
| `School Entrance Portal` (`school_entrance`) | inside the school's south entrance lobby | Returns to the foyer |
| `School Portal` (`school_gate`) | inside `Main Building Room` (foyer) | Sends the player to the school entrance |
| `Room Marker <room>` + `SchoolRoomMarker` | one per principal room | Reachability test anchors |

Because both portals are in the same scene, `Destination Scene` is **blank** on both and
`Changes World` is off. `PortalManager` finds the destination portal by id in the active scene and
teleports the player to its `Exit Point`; nothing is loaded or reloaded, so the player, camera,
inventory and World A state are preserved. Each arrival point sits outside its portal's trigger, so
there is no bounce-back.

The foyer keeps its existing content, including the `world_b_portal` (World B) portal and the
`main_building_door` ⇄ `int_20_14` route back to the Town.

## Layout

Abstracted from the Akron Community School floor plan (entrance to the south):

- **Southwest:** middle-school classrooms.
- **Far west:** chemistry/physics and biology labs.
- **Northwest:** high-school classrooms, sewing, cooking, living & learning, high-school lab.
- **Central-west:** auditorium with a stage, plus administrative and nurse's offices.
- **North-central:** dining/commons; **central:** commons.
- **North:** art, library, study hall.
- **Northeast:** industrial-arts and vocational-agriculture shops.
- **East-central:** gymnasium with court markings, hoops, bleachers; boys'/girls' locker rooms along
  its east side; kitchen and staff room to the west.
- **Southeast:** vocal and instrumental music rooms and a wrestling/apparatus room.

Corridors form one connected network. Each room opens onto a corridor through a genuine doorway
(two cells wide, cut at the middle of each room↔corridor boundary). Rooms are separated from each
other by solid wall cells. Tiny storage rooms and unreadable details from the plan are simplified.

## Presentation

Matches the Town interior:

- Coloured floor meshes per room; grey corridor floors; a ground plane under the school area.
- Walls are height-1 boxes on the `Walls` layer (thick 0.3), so the player stays visible; there are
  no tall foreground walls.
- Props are simple coloured boxes (`Prop`) with `BoxCollider`s on the `Walls` layer: desks,
  bookshelves, stage, seating, counters, court markings, bleachers, lockers, music/wrestling
  equipment. Court markings and the wrestling mat are collider-free flat quads.
- Four wandering `Student` NPCs populate the corridors.

## Playable behavior

- The existing Town `PlayerController3D` and `IsoCameraRig` are used unchanged; there is no school
  player, camera, light or world identity.
- Entering the foyer's `School Portal` teleports on contact (same-scene, same-world); entering the
  school's `School Entrance Portal` teleports back to the foyer.

## Generation workflow

`SchoolInteriorBuilder` (editor-only) generates the school under a new `School Interior` root at the
origin; the Town builder moves the root to `(100, 0, 0)`. Every object is parented under the root, so
the school can be repositioned by moving it.

Two menu items keep the saved scene in sync:

1. **Tools > Worlds > Rebuild Town Scene (3D)** — full Town rebuild; includes the school.
2. **Tools > Worlds > Town 3D > Refresh School Interior** — rebuilds/replaces just the school inside
   the open Town scene (fast path when only the school layout changed).
3. **Tools > Worlds > Town 3D > Refresh Foyer Portals** — recreates the foyer's `World B Portal` and
   `School Portal` (and their colour pads) in the open scene.

Hand edits survive a rebuild through the Town rebuild point
(**Tools > Worlds > Town 3D > Set Rebuild Point**; see [TOWN_REBUILD_POINT.md](TOWN_REBUILD_POINT.md)).

## Verification

- `SchoolPortalPlayModeTests` checks the same-scene portal pair, that a round trip does not change
  the active scene handle, that arrivals land on the exit points (foyer arrival inside the main
  building room), the untouched `main_building_door` ⇄ `int_20_14` Town connection, and that every
  room marker is physically reachable from the school entrance.
- `Tools/verify-smoke.ps1` and `SceneUiCameraPlayModeTests` cover Town's camera/UI invariants, which
  now include the school content.
