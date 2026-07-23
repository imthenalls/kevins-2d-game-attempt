# Script and Runtime API Reference

This index covers every C# script under `Assets/Scripts/`. “Key runtime API” lists the public members intended for calls from other systems. Unity lifecycle methods and internal implementation helpers remain documented in each script's XML comments.

For Inspector wiring and scene setup, use the linked system documents in `AGENT.md`.

## Entity

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/Entity/CharacterStatistics.cs` | Tracks attacks, damage, kills, critical hits, gathered items, and money. | Read-only totals; `RecordCriticalHit()`, `RecordKill()`, `RecordItemGathered()`, `RecordMoneyGained()`; per-stat change events |
| `Assets/Scripts/Entity/CombatAttacker.cs` | Finds a nearby `CombatReceiver` and sends melee damage. | `TryAttack()`, `OnAttackLanded`, `OnKillLanded` |
| `Assets/Scripts/Entity/CombatReceiver.cs` | Accepts hits, applies damage to `EntityStats`, and reports hits/death. | `ReceiveHit()`, `CombatEnabled`, `Invincible`, `DamageMultiplier`, `Stats`, `OnHit`, `OnDeath` |
| `Assets/Scripts/Entity/DamageInfo.cs` | Value object describing one hit. | `DamageInfo(amount, source)`, `Amount`, `Source` |
| `Assets/Scripts/Entity/EntityStats.cs` | Owns HP, MP, equipment bonuses, and stat-change events. | `Configure()`, `TakeDamage()`, `Heal()`, `SetHp()`, `SpendMp()`, `RestoreMp()`, `SetMp()`, `IncreaseMaxHp()`, `IncreaseMaxMp()`, `ApplyStatBonus()`, `RemoveStatBonus()` |
| `Assets/Scripts/Entity/EntityStatsUI.cs` | Displays an `EntityStats` component through HP and MP image fills. | Event-driven component; no public methods |
| `Assets/Scripts/Entity/IEntityController.cs` | Shared player/NPC controller contract. | `DisplayName`, `Stats`, `CombatReceiver`, `MovementEnabled`, `SetMovementEnabled()` |

## Game Management

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GameManagement/RuleTrigger2D.cs` | Changes one scene-rule boolean in response to a configured 2D trigger event. | Inspector-driven component |
| `Assets/Scripts/GameManagement/RuleZone2D.cs` | Pushes and removes a temporary `SceneRules` override while an activator is inside a zone. | Inspector-driven component |
| `Assets/Scripts/GameManagement/SaveData.cs` | Serializable save-file DTOs for player, world, quests, NPCs, inventory, and hotbar state. | Public serialized fields on `SaveData` and its entry types |
| `Assets/Scripts/GameManagement/SaveManager.cs` | Saves and restores persistent game state. | `Instance`, `HasSave()`, `Save()`, `Load()` |
| `Assets/Scripts/GameManagement/SceneLoader.cs` | Loads/reloads scenes with a fade transition. | `Instance`, `IsLoading`, `LoadScene()`, `ReloadCurrentScene()` |
| `Assets/Scripts/GameManagement/SceneRules.cs` | ScriptableObject containing per-scene gameplay settings. | Public Inspector fields |
| `Assets/Scripts/GameManagement/SceneRulesManager.cs` | Applies base and override rules to the player, NPCs, inventory, portals, and periodic damage/healing. | `Instance`, `PushOverride()`, `PopOverride()`, rule setters, `RegisterNpc()` |

## Inventory, Equipment, Hotbar, and Loot

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/Inventory/Equipment/EquipmentManager.cs` | Connects an entity's equipment model to its stat bonuses. | `Model`, `Equip()`, `Unequip()` |
| `Assets/Scripts/Inventory/Equipment/EquipmentModel.cs` | Pure-data equipment-slot container. | `Equip()`, `Unequip()`, `GetEquipped()`, `IsSlotEmpty()`, `OnChanged` |
| `Assets/Scripts/Inventory/Equipment/EquipSlotType.cs` | Defines weapon, armor, and accessory slots. | Enum values |
| `Assets/Scripts/Inventory/HotbarModel.cs` | Stores six assigned quick-use items. | `GetSlot()`, `Assign()`, `Clear()`, `FirstEmptySlot()`, `OnChanged` |
| `Assets/Scripts/Inventory/HotbarSlotUI.cs` | Displays and handles interaction for one hotbar slot. | `Setup()`, `Refresh()`, pointer/drop handlers |
| `Assets/Scripts/Inventory/HotbarUI.cs` | Owns the shared hotbar and processes quick-use input. | `Model`, `AssignSlot()`, `ClearSlot()`, `AssignFirstEmpty()` |
| `Assets/Scripts/Inventory/InventoryContextMenu.cs` | Shows item actions for an occupied inventory slot. | `Show()`, `Hide()` |
| `Assets/Scripts/Inventory/InventoryHelper.cs` | Gives items while also updating statistics and quest events. | `GiveItem()` |
| `Assets/Scripts/Inventory/InventoryModel.cs` | Grid-based inventory data and stack operations. | `GetSlot()`, `AddItem()`, `RemoveItem()`, `HasItem()`, `CountItem()`, `MoveSlot()`, `SplitStack()`, `Sort()`, `ForceRefresh()`, `OnChanged` |
| `Assets/Scripts/Inventory/InventorySlot.cs` | Stores one item reference and quantity. | `IsEmpty`, `Set()`, `Clear()` |
| `Assets/Scripts/Inventory/InventorySlotUI.cs` | Displays one inventory slot and handles drag, drop, hover, and clicks. | `Setup()`, `Refresh()`, pointer/drag/drop handlers |
| `Assets/Scripts/Inventory/InventorySplitDialog.cs` | Lets the player select an exact stack-split quantity. | `Show()`, `Hide()`, `IsOpen` |
| `Assets/Scripts/Inventory/InventoryTooltip.cs` | Displays item details beside the cursor. | `Show()`, `Hide()` |
| `Assets/Scripts/Inventory/InventoryUI.cs` | Owns the player inventory model and slot grid. | `Instance`, `Model`, `IsOpen`, `InputLocked`, `Toggle()`, `Open()`, `Close()` |
| `Assets/Scripts/Inventory/ItemData.cs` | ScriptableObject definition for an item. | Public item fields, `IsEquip`, `IsStackable` |
| `Assets/Scripts/Inventory/ItemDatabase.cs` | Loads and registers item definitions by ID. | `Instance`, `Get()`, `TryGet()`, `Register()` |
| `Assets/Scripts/Inventory/ItemType.cs` | Defines broad item categories. | Enum values |
| `Assets/Scripts/Inventory/LootContainerUI.cs` | Displays and transfers items from a source inventory. | `Show()`, `Hide()`, `IsOpen` |
| `Assets/Scripts/Inventory/LootSlotUI.cs` | Displays and handles interaction for one loot slot. | `Setup()`, `Refresh()`, pointer handlers |

## NPCs and Dialogue

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/NPCs/DialogueData.cs` | Serializable dialogue graph, node, and choice DTOs. | Public serialized fields |
| `Assets/Scripts/NPCs/DialogueDatabase.cs` | Loads and indexes JSON/asset dialogue graphs. | `RegisterAsset()`, `TryGetDialogue()` |
| `Assets/Scripts/NPCs/DialogueGraphAsset.cs` | ScriptableObject wrapper for a dialogue graph. | `Graph`, `DialogueId` |
| `Assets/Scripts/NPCs/DialogueUIController.cs` | Displays the speaker, line, and choice list. | `GetOrCreate()`, `ShowDialogue()`, `HideDialogue()`, `IsShowingDialogue` |
| `Assets/Scripts/NPCs/INpcBehavior.cs` | Contract for pluggable NPC behaviors. | `Weight`, `OnEnter()`, `Tick()`, `OnExit()`, `IsComplete()` |
| `Assets/Scripts/NPCs/NpcBehaviorManager.cs` | Selects and runs `INpcBehavior` components. | Lifecycle-driven component |
| `Assets/Scripts/NPCs/NpcController.cs` | NPC identity, type, state, movement lock, combat references, and optional inventory. | Identity/state properties, `CanInteract()`, `SetBehaviorState()`, `SetMovementEnabled()` |
| `Assets/Scripts/NPCs/NpcDialogue.cs` | Connects an NPC to a dialogue graph and conversation state. | `CanStartDialogue()`, node lookup methods, `BeginConversation()`, `EndConversation()`, `SelectDialogue()` |
| `Assets/Scripts/NPCs/NpcIdleBehavior.cs` | Waits for a randomized duration. | `INpcBehavior` implementation |
| `Assets/Scripts/NPCs/NpcWanderBehavior.cs` | Walks toward randomized nearby destinations. | `INpcBehavior` implementation |

## Player

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/Player/PlayerController2D.cs` | Reads movement input and drives the player's `Rigidbody2D`. | `DisplayName`, `Stats`, `CombatReceiver`, `MovementEnabled`, `MoveSpeed`, `SetMovementEnabled()` |
| `Assets/Scripts/Player/PlayerInteractionController.cs` | Finds nearby NPC/world interactables and drives conversations/interactions. | Input- and lifecycle-driven component |

## Portals

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/Portals/Portal2D.cs` | Performs a direct same-scene teleport to a configured transform. | `BlockForSeconds()` |
| `Assets/Scripts/Portals/PortalData.cs` | Serializable portal configuration DTOs. | Public fields; `SerializableVector3.ToVector3()` |
| `Assets/Scripts/Portals/PortalManager.cs` | Loads portal definitions and coordinates same/cross-scene travel. | `Instance`, `TryUsePortal()`, `TryGetPortal()` |
| `Assets/Scripts/Portals/PortalSpawnPoint.cs` | Marks a named arrival location. | `SpawnId` |
| `Assets/Scripts/Portals/PortalTrigger2D.cs` | Starts manager-based or locally wired portal travel. | `PortalId`, `BlockForSeconds()` |

## Quests and Persistent State

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/Quests/ICondition.cs` | Defines and implements quest-transition conditions. | `Evaluate()` and condition constructors |
| `Assets/Scripts/Quests/IQuestAction.cs` | Defines and implements quest-node side effects. | `Execute()` and action constructors |
| `Assets/Scripts/Quests/QuestData.cs` | Serializable quest graph DTOs. | Public serialized fields |
| `Assets/Scripts/Quests/QuestEventBus.cs` | Broadcasts decoupled quest progress events. | `OnEvent`, `Raise()` |
| `Assets/Scripts/Quests/QuestInstance.cs` | Tracks the active nodes and objective counts of one quest. | State properties, `OnEvent()`, `TryAdvance()` |
| `Assets/Scripts/Quests/QuestLoader.cs` | Loads quest JSON and builds condition/action instances. | `LoadAll()`, `Load()`, `BuildCondition()`, `BuildAction()` |
| `Assets/Scripts/Quests/QuestManager.cs` | Owns active quests and quest save data. | `Instance`, `StartQuest()`, query methods, `LoadSaveData()`, `GetSaveData()` |
| `Assets/Scripts/Quests/WorldStateManager.cs` | Persistent singleton key/value store used by quests and world reactions. | Fact, flag, typed-value, snapshot APIs; `OnFlagChanged` |

## World Objects

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/World/EnemyLootPresenter.cs` | Seeds enemy loot and opens the loot panel after death. | Event-driven component |
| `Assets/Scripts/World/IInteractable.cs` | Contract for player-interactable world objects. | `CanInteract()`, `GetDisplayName()`, `TryGetCurrentLine()`, `Advance()`, `EndInteraction()` |
| `Assets/Scripts/World/ItemPickup.cs` | Transfers a placed item into inventory on player contact. | Trigger-driven component |
| `Assets/Scripts/World/LootContainer.cs` | Interactable chest/container with an inventory model. | `IInteractable` implementation |
| `Assets/Scripts/World/WorldObject.cs` | General-purpose line-based interactable object. | `IInteractable` implementation |

## World-State Reactions

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/WorldState/EnemyDeathFlagSetter.cs` | Sets a world-state flag when its enemy dies. | Event-driven component |
| `Assets/Scripts/WorldState/WorldStateActivator.cs` | Activates/deactivates a target according to a flag. | Inspector-driven component |
| `Assets/Scripts/WorldState/WorldStateDestroyer.cs` | Permanently destroys an object when a flag is set. | Inspector-driven component |
| `Assets/Scripts/WorldState/WorldStateDialogueSelector.cs` | Selects NPC dialogue according to world state. | `Key` |
| `Assets/Scripts/WorldState/WorldStateInteractable.cs` | Enables/disables guarded interactable components according to a flag. | Inspector-driven component |
| `Assets/Scripts/WorldState/WorldStateKey.cs` | Designer-friendly ScriptableObject key asset. | `Key`, implicit string conversion |
| `Assets/Scripts/WorldState/WorldStateNpcReactor.cs` | Moves or changes an NPC's state in response to a flag. | `Key` |
| `Assets/Scripts/WorldState/WorldStateSpawner.cs` | Spawns a configured prefab according to a flag. | Inspector-driven component |
