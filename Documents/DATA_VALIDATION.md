# Data Validation

String ids tie the game together: items, NPCs, dialogue, quests, portals, and loot all reference
each other by id. Those references are resolved at runtime, so a typo or a missing entry is
normally invisible until the game misbehaves. This tool checks them up front.

## Running it

- Menu: **Tools > Validation > Validate Game Data**
- From the Unity CLI: `unity command menu --path "Tools/Validation/Validate Game Data"`
- From code: `GameDataValidator.Validate()` (returns the issue count; 0 = clean)

It writes a full report to `Temp/game-data-validation.txt`, logs a one-line summary, and logs an
error if any errors were found.

## What it checks

| Area | Checks |
|---|---|
| Items (`Assets/StreamingAssets/items.json`) | blank and duplicate `id` |
| NPC inventories (`npc_inventories.json`) | blank/duplicate `npcId`; `itemId` references resolve |
| Enemy loot (`enemy_loot.json`) | blank/duplicate `npcId`; `itemId` references resolve |
| Dialogue (`dialogues.json`) | blank/duplicate `dialogueId`; blank/duplicate node `id`; `startNodeId` exists; every `nextNodeId` (node and choice) resolves unless the node/choice ends the conversation; `questId` references resolve; `teleportScene` references resolve |
| Quests (`Assets/StreamingAssets/quests/*.json`) | blank/duplicate `questId`; blank/duplicate node `id`; `startNodeId` exists; transition `targetNodeId` exists; condition `nodeId` exists; `itemId` and `questId` references resolve |
| Scenes (`Assets/Scenes/*`) | blank/duplicate `NpcController.NpcId`; portal `destinationPortalId` references resolve; exactly one `WorldSceneIdentity` when a `WorldCharacter` exists; a `PlayerSpawnPoint` exists (warns on duplicates); a `Grid` exists and every `Grid`/`Tilemap` is at the origin with identity rotation/scale; a `Camera` is tagged `MainCamera`; every Tilemap with a `TilemapCollider2D` is on the `Walls` layer |

All comparisons are case-insensitive, matching how the runtime resolves ids.

Scene checks open each scene in turn (the active scene setup is restored afterwards) and are
**skipped with a warning when any open scene has unsaved changes** â€” save first, then run.

## Adding a check

The pure id logic lives in `Game.Core.IdIntegrity` (`Assets/Scripts/GameData/IdIntegrity.cs`) and is
unit tested in `Assets/Tests/EditMode/IdIntegrityTests.cs` (mirrored by `dotnet test`). Add new
reference checks by feeding `IdIntegrity.IdReference` pairs into
`FindDanglingReferences`, or id lists into `FindDuplicateOrBlankIds`.

The Editor side (`Assets/Scripts/GamePresentation/Editor/Validation/GameDataValidator.cs`) handles
file/scene discovery and JSON parsing. Its DTOs mirror the StreamingAssets JSON shapes, so keep them
in sync if those files change.

## Known findings

All findings from the first run have been fixed:

- Items `bounty_gold` and `evidence_letter` were added to `items.json` (referenced by quest
  `bandit_king`'s `ending_kill` / `ending_expose` nodes).
- Quest `sheriffs_gratitude` was authored in `Assets/StreamingAssets/quests/` â€” it is started by
  `bandit_king`'s `ending_expose` and rewards two health potions for reporting the exposure.
- The duplicate Overworld NPC id `sword_guard` was resolved by renaming the clone
  ("sword guard npc (1)") to `sword_guard_2`; it received its own `enemy_loot.json` entry so it
  drops the same loot as the original. Old saves that keyed either NPC as `sword_guard` keep the
  first guard's state; the clone is a new save key.

A clean validation run now reports `errors=0 warnings=0`.

The scene-identity check was added after Overworld was found to have a `WorldCharacter` but no
`WorldSceneIdentity`: a direct (non-portal) load of Overworld while `WorldTravelState.CurrentWorld`
was World B would deactivate the World A player. Overworld now carries a `WorldSceneIdentity`
(World A) on a root named "World A", mirroring World B's "World B" root.
