# TODO

## Completed

- [x] **HP / MP** — `EntityStats.cs` + `EntityStatsUI.cs`
- [x] **NPC System** — `NpcController`, `NpcBehaviorManager`, `NpcIdleBehavior`, `NpcWanderBehavior`, dialogue stack
- [x] **Dialogue with NPCs** — `DialogueUIController`, `NpcDialogue`, `DialogueData/Database/GraphAsset`
- [x] **Quest System** — `QuestManager`, `QuestInstance`, `QuestLoader`, `QuestEventBus`, `WorldStateDB`
- [x] **Inventory** — `InventoryModel`, `InventoryUI`, `InventorySlotUI`, `InventoryTooltip`, `InventoryContextMenu`, `ItemData`, `ItemDatabase`
- [x] **Scene Manager** — `SceneLoader.cs` with fade transition; `PortalManager` routes cross-scene travel through it
- [x] **Combat** — `DamageInfo`, `CombatReceiver`, `CombatAttacker`; damage via `EntityStats.TakeDamage`; fires `QuestEventBus.Raise("EnemyKilled", id)` on enemy death
- [x] **Save / Load** — `SaveManager.cs` + `SaveData.cs`; serializes scene, player position, HP/MP, WorldStateDB facts, quest states, and inventory to `save.json`
- [x] **Scene Rules** — `SceneRules` (ScriptableObject), `SceneRulesManager`, `RuleZone2D`, `RuleTrigger2D`; per-scene overrides for inventory lock, combat toggles, invincibility, DOT, and movement lock

---

## In Progress / Next

- [ ] **Sprite flipping on NPC wander** — `NpcWanderBehavior` moves the NPC but never flips the sprite to face direction of travel

---

## Backlog

- [ ] **Stack splitting** — Shift+click to split a stack in the inventory
- [ ] **Item rarity tiers** — Common / Uncommon / Rare / Unique with slot border color
- [x] **Equipment slots** — `EquipmentManager`, `EquipmentModel`, `EquipSlotType`; weapon/armor/accessory per entity; stat bonuses applied to EntityStats
- [ ] **Hotbar** — 4-6 quick-use slots for consumables, usable without opening inventory
- [ ] **Loot / container panel** — Mirrored grid UI when opening chests or looting enemies, with Take All button