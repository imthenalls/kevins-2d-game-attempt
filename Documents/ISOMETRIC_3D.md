# Isometric 3D Migration

The game is moving from flat top-down 2D to a **3D planar-isometric** presentation: real meshes,
a pitch/yaw isometric camera, 3D colliders, and billboarded sprites for actors. The reference look
is a Mega Man Battle Network style isometric field, but rendered with actual 3D geometry so terrain
can have height (cliffs, stairs, blocky buildings).

`Town` is the pilot scene. Other scenes (`Overworld`, `WorldB`) are still 2D until converted.

## The core rule

**Gameplay is planar and verticality-free; the world is 3D.** Actors live on the XZ ground plane,
Y is up, and the Rigidbody has `FreezePositionY`. There is no jumping. Everything that used to be
"XY" (movement, pathfinding, distances) now runs on "XZ".

The authoritative state still lives in `Game.Data` (`PositionModel`, `PlayerMovementConfig`, etc.);
only the presentation axis changed. Shared systems talk to the player through `PlayerControllerBase`
instead of `PlayerController2D`.

## Components

| Script | Role |
|---|---|
| `PlayerControllerBase` | Abstract player contract (`Stats`, `ManaWallet`, `MoveSpeed`, `SetMovementEnabled`, `ApplyAvatarProfile`, position model). `PlayerController2D` and `PlayerController3D` derive from it. |
| `PlayerController3D` | Planar XZ `Rigidbody` controller: camera-relative WASD, dash, health/position model. |
| `IsoCameraRig` | Keeps a 3D orthographic camera at a fixed isometric pitch/yaw above the player. Added by `GameBootstrap` for 3D scenes. |
| `BillboardSprite` | Turns a `SpriteRenderer` to face the camera so sprites stay upright and readable. |
| `Isometric3DScene` | Marker on a scene root. Makes `GameBootstrap` add `IsoCameraRig`, and makes the data validator skip 2D-only checks (Grid/tilemap/walls tilemap). |
| `Isometric3DRendererSetup` | Creates/loads `Assets/Settings/Renderer3D.asset` (a URP `UniversalRendererData`) and registers it in `UniversalRP.asset`. |
| `Isometric3DSceneView` | Editor menu: snaps the Scene view to the isometric angle and frames the scene. |

## Rendering

- A 3D URP renderer (`Renderer3D.asset`) is added to the URP asset's renderer list. 3D scenes select
  it **per camera** via `UniversalAdditionalCameraData.SetRenderer(index)`, so 2D scenes keep
  `Renderer2D`. See `Town3DSceneBuilder.BuildCamera`.
- Placeholder scenes use **flat ambient light** and diffuse-only Lit materials so colours read
  predictably. See `ConfigurePlaceholderLighting` / `ApplyMaterial`.

## Portals (2D + 3D)

`IPortalRoute` is the shared routing surface. `PortalTrigger2D` and `PortalTrigger3D` both implement
it, and `PortalManager` routes either. `PortalTrigger3D` uses a 3D `Collider` and `OnTriggerEnter`.

Same-scene portal ids:

- Town door: `door_<x>_<y>` → destination `int_<x>_<y>`; Exit Point just outside the building.
- Room portal: `int_<x>_<y>` → destination `door_<x>_<y>`; Exit Point inside the room.

Same-world routes activate on contact; world-changing routes require **G**.

## Town scene (`Town3DSceneBuilder`)

Menu: **Tools > Worlds > Rebuild Town Scene (3D)** (rebuilds `Assets/Scenes/Town.unity` in place).

- Ground/roads/park are meshes on XZ; buildings are extruded boxes with `BoxCollider` on the
  `Walls` layer.
- **Interiors:** one room per building, south of town at varying sizes and shapes (some L-shaped
  from two rectangles). Walls enclose each room, merging collinear edges into single boxes.
  Each room has a pink room door that routes back to town.
- **Doors:** the pink town door is a billboarded sprite; a separate non-rotating trigger child
  carries the `BoxCollider` + `PortalTrigger3D` and an `Approach` exit.

## NPC schedule

| Script | Role |
|---|---|
| `NpcWander3D` | 3D wanderer: random XZ targets, sphere-cast wall avoidance. |
| `NpcPathfinder3D` | Grid A* on XZ; blocked cells found with `Physics.CheckBox` on the Walls layer. |
| `NpcSchedule3D` | Daily loop: wander → walk to its house door (pathfind) → teleport inside → wait → come back out. Disables `NpcWander3D` while commuting. Carries a home key id. |

All 14 town villagers are assigned a home (`door_<x>_<y>` / `int_<x>_<y>`), one per building, and each
holds its `house_key_<x>_<y>` **item** in its inventory (the ids are defined in `items.json` as KeyItems
and seeded per villager from `npc_inventories.json`).
Town doors are **dynamic**: the owner's `NpcSchedule3D` unlocks the door while it is `Home` and relocks
it when it leaves. A locked door requires `house_key_<x>_<y>`: `PortalManager` accepts the key from an
`IKeyHolder` keyring **or** a real item in the traveler's inventory, so the owner passes with its
inventory key while the player needs the key (resolved from `PlayerKeyring`). A building is only freely
enterable while its owner is home (the door tints green when open, pink when locked).
Enemies: two bandits (`NpcProximityMelee3D`), a `Town Brute` (high-HP proximity melee) and a
`Town Dasher` (`NpcDashMelee3D`).

**NPC state (Engine-Free Core):** every town NPC and enemy carries `NpcStateView`, so its `Hp/MaxHp`
and logical cell live in the `Game.Core.NpcState` model (via `GameSession.NpcStates`) and are saved.
`NpcStateView` is dimension-aware — 3D NPCs map cells on XZ with `Cell Size` (no `Grid`). The home
schedule (phase + seconds remaining) is likewise authoritative in `Game.Core.NpcScheduleState`
(`GameSession.NpcSchedules`, saved per NPC); `NpcSchedule3D` is a thin facade that reads/writes it, so
"who is home" survives a save.

## Verification

- `Tools/verify-smoke.ps1` loads every scene in Play Mode and fails on console errors.
- `Tools > Validation > Validate Game Data` reports 0 errors; Town's only warnings are the expected
  missing EventSystem / InventoryUI (the boot layer supplies them).
- `Tools/verify-all.ps1` runs the full suite.

## Interaction & dialogue

`PlayerInteractionController` is dimension-agnostic: its target search uses `Physics2D.OverlapCircle`
when the player has a `Rigidbody2D` and `Physics.OverlapSphere` when it has a `Rigidbody` (see
`CollectTargets`). The dialogue graph, UI and input paths were already engine-neutral, so they work
unchanged in 3D. `NpcController.CanInteract` measures planar distance on the scene's gameplay plane
(XY for 2D, XZ for 3D) by checking for a `Rigidbody2D`.

Town NPCs carry `NpcDialogue` (`town_villager_a` / `town_villager_b` in `dialogues.json`) and the
3D player has a `PlayerInteractionController`; the 3D wanderer/schedule hold still while
`NpcController.BehaviorState` is `Talking`.

## Combat

`CombatAttacker` / `CombatReceiver` / `EntityStats` are dimension-agnostic and reused as-is; only
the weapon presentation and hit detection needed a 3D form.

| Script | Role |
|---|---|
| `EquippedWeaponVisual3D` | 3D counterpart of `EquippedWeaponVisual`: billboarded weapon that orbits the character, sweeps in a horizontal arc, and offers any `CombatReceiver` inside a frontal cone to `CombatAttacker.TryApplyWeaponHit`. The swing aims along the owner's facing (player: last movement; NPC: toward the player). |
| `NpcProximityMelee3D` | 3D counterpart of `NpcProximityMeleeController`: chases the player on XZ, stops in range and swings. Holds the NPC in `Combat` so the wanderer pauses, and returns it to `Idle` when the player leaves. |
| `NpcProximityMelee3D` | 3D counterpart of `NpcProximityMeleeController`: chases the player on XZ, stops in range and swings. Holds the NPC in `Combat` so the wanderer pauses, and returns it to `Idle` when the player leaves. |
| `NpcDashMelee3D` | 3D counterpart of `NpcDashMeleeController`: approach → warning flash (body tints) → straight XZ dash (sphere-cast, so walls stop it) → swing → recovery. Uses the same `NpcDashMeleeConfig`. |

- Player attack input (Space) is ignored while movement is locked (`CombatAttacker` now requires
  `PlayerControllerBase.MovementEnabled`), so it does not fire during dialogue or the inventory.
- `EntityStats.EnsureInitialized()` makes HP initialization independent of Unity's Awake order, so a
  controller can safely read `stats.MaxHp/Hp` while seeding the shared health model.
- Town has two wandering bandits with `NpcWander3D` + `NpcProximityMelee3D`; the player carries an
  iron sword via `EquipmentManager` starting loadout.
- **Loot:** `EnemyLootDrop`, `RuntimeEnemyLootPile` and `ItemPickup` are dimension-aware. A 3D drop
  is a billboarded pile with a 3D trigger `BoxCollider`, collected with **E** (same interaction path
  as 2D). `town_bandit_1` / `town_bandit_2` have entries in `enemy_loot.json`.

## Known limitations

- Only `Town` is converted. `Documents/ISOMETRIC_CONVERSION.md` describes the earlier 2D fake-iso
  approach and is superseded by this document for 3D scenes.
- The 2D NPC pathfinder/behaviors and `NpcDashMeleeController` remain `Physics2D`; 3D scenes use the
  3D wanderer/schedule/melee stack instead.
- Opening the scene: use **Tools > Worlds > Isometric 3D > Focus Scene View** for a clean view.
