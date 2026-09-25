# Project Review and Recommended Next Steps

## Overall Opinion

The project has a strong foundation, especially for a solo Unity RPG prototype. The systems are separated sensibly, the code is unusually well documented, and there is a coherent architecture connecting combat, inventory, quests, saving, portals, NPCs, and world state.

The main concern is scope. A large amount of infrastructure has been built, but there is relatively little evidence that all of it has been exercised together as a complete game loop. At this stage, the project feels more like an RPG framework than a playable RPG.

### Strongest Areas

- Clear system boundaries and folder organization
- Shared entity abstractions
- Data-driven quests, dialogue, portals, and items
- Persistent world-state integration
- Good Inspector and setup documentation
- Reusable inventory, equipment, loot, and scene-rule systems

### Main Risks

- Many global singletons and static access points
- Test coverage has grown (unit, contract, and Play Mode) but still thins out at system boundaries
- Systems may work individually but have limited end-to-end coverage (one Play Mode wiring test exists; no full interaction flow yet)
- JSON IDs, scene names, item IDs, dialogue IDs, and portal IDs can fail only at runtime
- Runtime-generated UI and automatic component discovery can hide configuration problems
- Save compatibility may become difficult as data structures evolve
- Features may accumulate faster than playable content

The project is a promising and thoughtfully designed prototype, but the next milestone should prove that it is a game rather than add another subsystem.

## Recommended Development Approach

Do not rewrite the project or replace the overall architecture. The current architecture is suitable for this stage.

The development process should change:

- Freeze new major systems temporarily.
- Build one complete vertical slice using the systems that already exist.
- Fix problems encountered during actual play instead of continuing to generalize the framework.
- Add tests around the integration points that break during the vertical slice.
- Introduce new abstractions only when an existing dependency causes a concrete problem.

A large dependency-injection framework or event-driven rewrite is not recommended right now. The current singletons are acceptable for a prototype. Direct singleton access can be reduced gradually through a bootstrap or service layer if it becomes a practical problem.

For important production UI, prefer authored Unity prefabs over relying primarily on runtime-generated fallback UI. Automatic UI creation is useful for testing and resilience, but explicit prefabs provide better visual control and expose missing references earlier.

## Recommended Next Steps

### 1. Repository and Unity Health

1. Open the project in Unity and force a complete refresh.
2. Resolve every Console error and warning.
3. Let Unity generate any missing `.meta` files and commit them.
4. Inspect scenes and prefabs for `Missing Script` components.
5. Run the full verification suite: `powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1`
   (compile, Edit Mode + Play Mode tests, `dotnet test`, and a scene smoke test).
6. Commit the class-declaration, duplicate-class, GUID, and documentation repairs as one recovery commit.

### 2. Build a Vertical Slice

Create a short 10–15 minute playable sequence:

1. Load into a small town.
2. Speak to an NPC.
3. Accept a quest.
4. Travel through a portal.
5. Fight one enemy.
6. Loot an item.
7. Equip or use the item.
8. Update the world based on the quest result.
9. Return to the NPC and complete the quest.
10. Save, quit, reload, and verify that the completed state persists.

This sequence exercises nearly every major system in the repository.

### 3. Add Integration Coverage

Prioritize tests for (status as of the save/quest hardening pass):

- ✅ Inventory stacking, splitting, moving, sorting, and removal (`InventoryModelTests`)
- ✅ Equipment bonus application and removal (`EquipmentIntegrationPlayModeTests`, `EquipmentRestorePlayModeTests`)
- ✅ Combat damage, invincibility, death, and quest events (`CombatIntegrationPlayModeTests`)
- ✅ Quest progression across multiple objectives (`QuestProgressionTests`)
- ✅ Save/load round trips (`SaveDataRoundTripTests`, `CompleteSequencePlayModeTests`)
- ⬜ Hotbar save restoration
- ✅ Portal destination resolution (`SystemIntegrationPlayModeTests`)
- ✅ World-state restoration after scene loading (`SystemIntegrationPlayModeTests`)
- ✅ Engine-free config defaults and the `ItemData`↔`IItem` contract

Additional coverage added since:

- ✅ Quest chaining (follow-up starts once) — `QuestChainingPlayModeTests`
- ✅ Undelivered quest rewards retained as pending — `PendingRewardPlayModeTests`
- ✅ Trade-state / ledger / wallet persistence — `TradePersistenceTests`
- ✅ Save validation and corruption recovery — `SaveSafetyPlayModeTests`

A Play Mode test now loads the real scenes and asserts the session boots and NPC models register
(`SceneIntegrationPlayModeTests`); a full interaction flow is still outstanding.

Run everything with `powershell -ExecutionPolicy Bypass -File Tools/verify-all.ps1`.

### 4. Add Data Validation

`Tools > Validation > Validate Game Data` (`GameDataValidator`) now covers most of this; see
[DATA_VALIDATION.md](DATA_VALIDATION.md). A reflection guard also fails the build on duplicate
serialized field names (`DuplicateSerializedFieldTests`).

Covered:

- Duplicate or empty NPC, item, dialogue, and quest ids
- Broken dialogue node links
- Missing quest transition targets / quest node references
- Dangling item and quest references from dialogue, quests, NPC inventories, and enemy loot
- Portal destination references and scene names

Still outstanding:

- Unknown quest action or condition types
- Missing spawn-point IDs
- Missing dialogue ids referenced by `NpcDialogue`

### 5. Stabilize Saving

Done (see [SAVE_SYSTEM.md](SAVE_SYSTEM.md)):

- ✅ Save-format version (currently 8), with per-version migration.
- ✅ Older saves migrate by subtracting equipment bonuses / merging legacy mana.
- ✅ Corrupt or partially written save files: validation + `save.json.bak` recovery.
- ✅ Write to a temporary file and replace `save.json` only after serialization succeeds.
- ⬜ Add a new-game/reset-save flow.
- ⬜ Test loading after a scene or item has been renamed.

### 6. Focus on Content and Game Feel

Once the vertical slice is reliable:

- Improve combat feedback, hit reactions, animation, sound, and enemy behavior.
- Build the inventory item-detail panel listed in the roadmap.
- Add attack types or status effects only if the actual combat design needs them.
- Create real quests, dialogue, encounters, and locations.
- Profile a development build on the intended target platform.

## Guiding Rule

Do not add a new foundational system unless the vertical slice cannot be completed without it.
