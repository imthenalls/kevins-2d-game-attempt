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

- [x] **Sprite flipping on NPC wander** — `NpcWanderBehavior` now flips `SpriteRenderer.flipX` based on horizontal movement direction

---

## Backlog

- [x] **Stack splitting** — `InventorySplitDialog`; Shift+click or context menu opens a slider to choose exact split amount
- [x] **Equipment slots** — `EquipmentManager`, `EquipmentModel`, `EquipSlotType`; weapon/armor/accessory per entity; stat bonuses applied to EntityStats
- [x] **Hotbar** — `HotbarModel`, `HotbarUI`, `HotbarSlotUI`; 6 slots, keys 1–6 to use; drag from inventory to assign; right-click to clear; healHp/healMp on ItemData; saves with the game
- [x] **Loot / container panel** — `LootContainerUI`, `LootSlotUI`, `LootContainer`, `EnemyLootPresenter`; chest and enemy loot with Take All button