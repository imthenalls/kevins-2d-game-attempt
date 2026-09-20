# Scene Checklist

Every gameplay scene must satisfy these. Most are enforced automatically; the "Guarded by" column
says where a violation will show up, so you fix the scene instead of discovering it in play.

## Mandatory scene content

| Requirement | Why | Guarded by |
|---|---|---|
| `Grid` at the origin, isometric cell `1 x 0.5`, tilemaps at the origin | Tile alignment; see [TILEMAP_RULES.md](TILEMAP_RULES.md) | Data validator (error) + tilemap alignment tool |
| One `WorldSceneIdentity` | A direct scene load must not deactivate the player | Data validator (error/warning) |
| One `PlayerSpawnPoint` (if the scene has a `WorldCharacter`) | The player needs a start position | Data validator (error) |
| A `PlayerController2D` where a spawn point exists | There must be something to spawn | Data validator (error) |
| A `Camera` tagged `MainCamera` | Something has to render | Data validator (error) |
| Collider tilemaps on the `Walls` layer | Obstacles must block pathfinding | Data validator (error) |

## Supplied by the boot layer (do not duplicate)

`GameBootstrap` runs before any scene loads and creates the persistent `Game Systems` root. See
[GAME_BOOTSTRAP.md](GAME_BOOTSTRAP.md). It provides, so a scene does **not** need to:

| Supplied | Notes |
|---|---|
| `GameSessionHost`, `SaveManager`, `PortalManager`, `SceneLoader`, `QuestManager`, `WorldStateManager` | A duplicate left in a scene destroys itself |
| A single **follow camera** | One enabled `MainCamera` camera is kept (preferring the object named `Main Camera`), the rest are disabled, and `CameraFollow` is added if missing |
| An **EventSystem** | Enabled only when the scene has none |
| A shared **inventory canvas** | `Assets/Resources/InventoryCanvas.prefab` is instantiated for any scene that authors no `InventoryUI`, so the `I`/`E` hotkeys work everywhere |

## Camera rules

1. A scene may contain more than one camera, but **at most one enabled camera tagged `MainCamera`**.
   The bootstrap disables and untags the extras at runtime, preferring the object named `Main Camera`.
2. Do not place a static `MainCamera` camera with a **higher `depth`** than the follow camera — it
   renders on top and the player sees a frozen view. Untag it (`Untagged`) or disable it.
3. Do not add a `CameraFollow` by hand; the bootstrap adds it. If you must place one, it is harmless
   (the bootstrap detects and keeps it).

## UI rules

1. A scene either **authors its own `InventoryCanvas`** (like Overworld and WorldB) or **relies on
   the shared one** (like Town). Both are valid; the persistent UI layer only fills the gap.
2. `InputLocked` on `InventoryUI` must be `false` unless a `SceneRules` asset locks it on purpose.
   A stuck lock makes every UI hotkey silently do nothing.
3. UI keyboards are read from the **Input System package** (`Keyboard.current`). If Unity's *Active
   Input Handling* is set to the legacy manager only, all UI hotkeys stop working.

## How to verify a scene

1. **Scene smoke test** (fastest): `Tools/verify-smoke.ps1` loads every scene in Play Mode and fails
   on any console error.
2. **Play Mode guards**: `Tools/verify-all.ps1` runs `SceneUiCameraPlayModeTests`
   (`Assets/Tests/PlayMode/SceneUiCameraPlayModeTests.cs`), which loads every scene and asserts:
   exactly one enabled follow camera, a live `EventSystem`, a keyboard device, a working
   `InventoryUI` panel, and a camera that converges on the player.
3. **Data validator**: `Tools > Validation > Validate Game Data` reports a warning for a missing
   `EventSystem` / `InventoryUI` (expected in scenes that rely on the boot layer) and for multiple
   enabled `MainCamera` cameras.
4. **Shared prefab guard**: `SharedInventoryCanvasPrefabTests` fails at edit time if
   `Resources/InventoryCanvas.prefab` is missing or loses its wired `InventoryUI`.

## Registering a new scene

1. Build the scene content from the table above.
2. Add the scene to **Build Settings** (the smoke test and Play Mode guards only cover listed scenes).
3. Add its path to the `Scenes` array in `Assets/Tests/PlayMode/SceneUiCameraPlayModeTests.cs` so the
   guards cover it.
4. Run `Tools/verify-all.ps1`.
