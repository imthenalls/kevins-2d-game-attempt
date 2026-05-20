# TODO

## Completed

- [x] **HP / MP** — `EntityStats.cs` + `PlayerStatsUI.cs`
- [x] **NPC System** — `NpcController`, `NpcBehaviorManager`, `NpcIdleBehavior`, `NpcWanderBehavior`, dialogue stack
- [x] **Dialogue with NPCs** — `DialogueUIController`, `NpcDialogue`, `DialogueData/Database/GraphAsset`
- [x] **Quest System** — `QuestManager`, `QuestInstance`, `QuestLoader`, `QuestEventBus`, `WorldStateDB`
- [x] **Inventory** — `InventoryModel`, `InventoryUI`, `InventorySlotUI`, `InventoryTooltip`, `InventoryContextMenu`, `ItemData`
- [x] **Scene Manager** — `SceneLoader.cs` with fade transition; `PortalManager` routes cross-scene travel through it

---

## In Progress / Next

- [ ] **Sprite flipping on NPC wander** — `NpcWanderBehavior` moves the NPC but never flips the sprite to face direction of travel

---

## Backlog

- [ ] **Save / Load** — Serialize `WorldStateDB.facts` and active `QuestInstance` state (node IDs + objective counts) to a save file
- [ ] **Combat** — Basic attack, hit detection, damage via `EntityStats.TakeDamage`; fire `QuestEventBus.Raise("EnemyKilled", id)` on death
- [ ] **Stack splitting** — Shift+click to split a stack in the inventory
- [ ] **Item rarity tiers** — Common / Uncommon / Rare / Unique with slot border color
- [ ] **Equipment slots** — Separate panel (weapon, armor, accessory) alongside the inventory grid
- [ ] **Hotbar** — 4-6 quick-use slots for consumables, usable without opening inventory
- [ ] **Loot / container panel** — Mirrored grid UI when opening chests or looting enemies, with Take All button