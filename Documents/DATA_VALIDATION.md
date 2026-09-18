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
| Scenes (`Assets/Scenes/*`) | blank/duplicate `NpcController.NpcId`; portal `destinationPortalId` references resolve |

All comparisons are case-insensitive, matching how the runtime resolves ids.

Scene checks open each scene in turn (the active scene setup is restored afterwards) and are
**skipped with a warning when any open scene has unsaved changes** — save first, then run.

## Adding a check

The pure id logic lives in `Game.Core.IdIntegrity` (`Assets/Scripts/GameData/IdIntegrity.cs`) and is
unit tested in `Assets/Tests/EditMode/IdIntegrityTests.cs` (mirrored by `dotnet test`). Add new
reference checks by feeding `IdIntegrity.IdReference` pairs into
`FindDanglingReferences`, or id lists into `FindDuplicateOrBlankIds`.

The Editor side (`Assets/Scripts/GamePresentation/Editor/Validation/GameDataValidator.cs`) handles
file/scene discovery and JSON parsing. Its DTOs mirror the StreamingAssets JSON shapes, so keep them
in sync if those files change.

## Known findings

As of the last run the validator reports genuine content issues that were left for a content pass:

- Quest `bandit_king` references item ids `bounty_gold` and `evidence_letter` that are not in `items.json`.
- Quest `bandit_king` references quest id `sheriffs_gratitude`, which is not defined.
- `Overworld` contains two NPCs with the id `sword_guard` (a known pre-existing issue; see
  [MODEL_VIEW_SLICE.md](MODEL_VIEW_SLICE.md) transitional compromise 3).
