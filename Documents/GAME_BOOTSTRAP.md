# Game Systems Bootstrap

Where the always-on managers live, so gameplay scenes only contain content.

## What happens on play

`GameBootstrap` (`Assets/Scripts/GamePresentation/GameManagement/GameBootstrap.cs`) runs
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` and creates one `DontDestroyOnLoad` root,
**`Game Systems`**, carrying:

| Persistent system | Provides |
|---|---|
| `GameSessionHost` | `GameSession` — NPC hp/cell models, player health, player position |
| `SaveManager` | save/load |
| `PortalManager` | portal/teleport travel |
| `SceneLoader` | scene loading used by portals |
| `QuestManager` | quests |
| `WorldStateManager` | world facts |

It uses `RuntimeInitializeOnLoadMethod` rather than a preload scene, so it also works when you press
**Play on any gameplay scene** in the editor (a preload scene would silently not run).

Already persistent the same way, independent of the bootstrap: `ItemDatabase`,
`NpcInventoryDatabase`, `WorldTravelState` (all auto-created), plus `PlayerKeyring.GetOrCreate()` and
some UI (`DialogueUI.GetOrCreate`).

## What a scene now needs

**Content only:**

- `Grid` at origin (isometric, cell `1 × 0.5`) + tilemaps
- a player spawn point
- `WorldSceneIdentity` (World A/B)
- a `Camera` tagged `MainCamera`
- NPCs, props, portals/doors, `SceneRulesManager`

**Do not add** `GameSessionHost`, `SaveManager`, `PortalManager`, `SceneLoader`, `QuestManager`, or
`WorldStateManager` to a scene — they are created by the bootstrap, and a duplicate placed in a
scene destroys itself.

## Rules and caveats

1. **Duplicates self-destruct.** Every persistent manager keeps its "if an instance exists, destroy
   self" guard. Because the bootstrap runs before scene load, a manager left in a scene loses.
2. **Rebind on scene load.** A persistent system that holds a scene reference must resolve it in
   `SceneManager.sceneLoaded`, not cache it in `Awake`. `PortalManager` and `WorldTravelState`
   already do this.
3. **State persists across Play Mode tests.** Use `PlayModeTestBase` (which resets `WorldTravelState`)
   and prefer the persistent instance (`WorldStateManager.Instance`, `PortalManager.Instance`) over
   creating your own.
4. **Scene-owned things still die with the scene**: `SceneRulesManager`, spawn points, grids, NPCs,
   portal/door instances.

## Verified

`Tools/verify-all.ps1` - the scene smoke test runs every scene through Play Mode with the bootstrap
active, and the Play Mode tests exercise the persistent `PortalManager` and `WorldStateManager`.

## Player spawn

`PlayerSpawnPoint` marks where the player starts. `GameBootstrap` reads it on scene load and places the
player there. Precedence for the player's position:

1. a save load (`SaveManager`) - applied after the scene loads, so it wins
2. a portal/travel position - applied on arrival, so it wins
3. `PlayerSpawnPoint` - the default first-entry start
4. the authored player transform - only if the scene has no spawn point

Each world scene has a `Player Spawn` object. The data validator errors if a scene with a
`WorldSceneIdentity` has no `PlayerSpawnPoint`, and warns if it has more than one.
