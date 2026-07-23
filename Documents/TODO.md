# Development Roadmap

Unchecked items are planned work. Completed work is retained below as a concise project-history checklist.

## Next

- [ ] **Inventory item-detail panel** — implement the Inspect action currently marked TODO in `InventoryContextMenu`; show the selected item's full description and relevant gameplay/equipment statistics.

## Backlog

- [ ] **Combat attack types, elements, and status effects** — extend `DamageInfo` and `CombatReceiver` with the hooks described in `COMBAT.md`.

## Completed

### Core systems

- [x] **HP / MP** — `EntityStats.cs` + `EntityStatsUI.cs`
- [x] **NPC system** — `NpcController`, `NpcBehaviorManager`, `NpcIdleBehavior`, and `NpcWanderBehavior`
- [x] **NPC dialogue** — `DialogueUIController`, `NpcDialogue`, and the dialogue data/database/asset stack
- [x] **Quest system** — `QuestManager`, `QuestInstance`, `QuestLoader`, `QuestEventBus`, and `WorldStateManager`
- [x] **Inventory** — model, UI, slots, tooltip, context menu, item data, and item database
- [x] **Scene management** — `SceneLoader` fade transitions and `PortalManager` cross-scene routing
- [x] **Combat** — `DamageInfo`, `CombatReceiver`, and `CombatAttacker`, including enemy-death quest events
- [x] **Save / load** — scene, player position, HP/MP, world-state facts, quests, inventory, NPCs, and hotbar state
- [x] **Scene rules** — base/override rules, trigger zones, combat toggles, invincibility, periodic effects, and movement locks

### Inventory and character additions

- [x] **Stack splitting** — exact split amount through `InventorySplitDialog`
- [x] **Equipment slots** — weapon, armor, and accessory equipment with `EntityStats` bonuses
- [x] **Hotbar** — six quick-use slots, inventory assignment, clearing, consumable use, and save integration
- [x] **Loot/container panel** — chest and enemy loot inventories with Take All
- [x] **Character statistics** — attacks, damage, kills, critical hits, gathered items, and money

### Presentation and behavior

- [x] **NPC wander sprite flipping** — `NpcWanderBehavior` updates `SpriteRenderer.flipX` from horizontal movement
