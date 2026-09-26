# Script and Runtime API Reference

This index covers every C# script under `Assets/Scripts/GamePresentation/`. “Key runtime API” lists the public members intended for calls from other systems. Unity lifecycle methods and internal implementation helpers remain documented in each script's XML comments.

For Inspector wiring and scene setup, use the linked system documents in `AGENT.md`.

## Economy

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/Economy/ITradeParticipant.cs` | Contract exposing a stable trader ID, Wallet, and InventoryModel. | `TradeParticipantId`, `TradeWallet`, `TradeInventory` |
| `Assets/Scripts/GamePresentation/Economy/TradeService.cs` | Validates and atomically commits player/NPC and NPC/NPC item-for-mana trades; owns the saved market ledger. | `TryExecute()`, `CompletedTrades`, `OnTradeCompleted`, `GetSaveData()`, `LoadSaveData()` |
| `Assets/Scripts/GamePresentation/Economy/Wallet.cs` | Owns canonical mana balance, capacity, and a bounded saveable transaction ledger. | `Balance`, `Capacity`, `RemainingCapacity`, `CanAfford()`, `CanReceive()`, `Add()`, `TrySpend()`, `TryTransferTo()`, `TryConsumeMana()`, `RestoreMana()`, `TrySubtract()`, capacity/balance controls, save-data methods, and change events |

## Entity

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/Entity/CharacterStatistics.cs` | Tracks attacks, damage, kills, critical hits, gathered items, and money. | Read-only totals; `RecordCriticalHit()`, `RecordKill()`, `RecordItemGathered()`, `RecordMoneyGained()`; per-stat change events |
| `Assets/Scripts/GamePresentation/Entity/CombatAttacker.cs` | Finds a nearby `CombatReceiver` and sends melee damage. | `TryAttack()`, `OnAttackLanded`, `OnKillLanded` |
| `Assets/Scripts/GamePresentation/Entity/CombatReceiver.cs` | Accepts hits, applies damage to `EntityStats`, and reports hits/death. | `ReceiveHit()`, `CombatEnabled`, `Invincible`, `DamageMultiplier`, `Stats`, `OnHit`, `OnDeath` |
| `Assets/Scripts/GamePresentation/Entity/DamageInfo.cs` | Value object describing one hit. | `DamageInfo(amount, source)`, `Amount`, `Source` |
| `Assets/Scripts/GamePresentation/Entity/EntityStats.cs` | Owns HP and equipment bonuses; exposes local MP or delegates MP to a bound canonical Wallet. | `Configure()`, `BindManaWallet()`, `TakeDamage()`, `Heal()`, `SetHp()`, `SpendMp()`, `RestoreMp()`, `SetMp()`, `IncreaseMaxHp()`, `IncreaseMaxMp()`, `ApplyStatBonus()`, `RemoveStatBonus()` |
| `Assets/Scripts/GamePresentation/Entity/EntityStatsUI.cs` | Displays an `EntityStats` component through HP and MP image fills. | Event-driven component; no public methods |
| `Assets/Scripts/GamePresentation/Entity/IEntityController.cs` | Shared player/NPC controller contract. | `DisplayName`, `Stats`, `CombatReceiver`, `MovementEnabled`, `SetMovementEnabled()` |

## Game Management

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/GameManagement/RuleTrigger2D.cs` | Changes one scene-rule boolean in response to a configured 2D trigger event. | Inspector-driven component |
| `Assets/Scripts/GamePresentation/GameManagement/RuleZone2D.cs` | Pushes and removes a temporary `SceneRules` override while an activator is inside a zone. | Inspector-driven component |
| `Assets/Scripts/GamePresentation/GameManagement/SaveData.cs` | Serializable save-file DTOs for player, world, quests, NPCs, inventory, and hotbar state. | Public serialized fields on `SaveData` and its entry types |
| `Assets/Scripts/GamePresentation/GameManagement/SaveManager.cs` | Saves and restores persistent game state. | `Instance`, `HasSave()`, `Save()`, `Load()` |
| `Assets/Scripts/GamePresentation/GameManagement/SceneLoader.cs` | Loads/reloads scenes with a fade transition. | `Instance`, `IsLoading`, `LoadScene()`, `ReloadCurrentScene()` |
| `Assets/Scripts/GamePresentation/GameManagement/SceneRules.cs` | ScriptableObject containing per-scene gameplay settings. | Public Inspector fields |
| `Assets/Scripts/GamePresentation/GameManagement/SceneRulesManager.cs` | Applies base and override rules to the player, NPCs, inventory, portals, and periodic damage/healing. | `Instance`, `PushOverride()`, `PopOverride()`, rule setters, `RegisterNpc()` |

## Inventory, Equipment, Hotbar, and Loot

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/Inventory/Equipment/EquipmentManager.cs` | Connects an entity's equipment model to its stat bonuses. | `Model`, `Equip()`, `Unequip()` |
| `Assets/Scripts/GamePresentation/Inventory/Equipment/EquipmentModel.cs` | Pure-data equipment-slot container. | `Equip()`, `Unequip()`, `GetEquipped()`, `IsSlotEmpty()`, `OnChanged` |
| `Assets/Scripts/GamePresentation/Inventory/Equipment/EquipSlotType.cs` | Defines weapon, armor, and accessory slots. | Enum values |
| `Assets/Scripts/GamePresentation/Inventory/HotbarModel.cs` | Stores six assigned quick-use items. | `GetSlot()`, `Assign()`, `Clear()`, `FirstEmptySlot()`, `OnChanged` |
| `Assets/Scripts/GamePresentation/Inventory/HotbarSlotUI.cs` | Displays and handles interaction for one hotbar slot. | `Setup()`, `Refresh()`, pointer/drop handlers |
| `Assets/Scripts/GamePresentation/Inventory/HotbarUI.cs` | Owns the shared hotbar and processes quick-use input. | `Model`, `AssignSlot()`, `ClearSlot()`, `AssignFirstEmpty()` |
| `Assets/Scripts/GamePresentation/Inventory/InventoryContextMenu.cs` | Shows item actions for an occupied inventory slot. | `Show()`, `Hide()` |
| `Assets/Scripts/GamePresentation/Inventory/InventoryHelper.cs` | Gives items while also updating statistics and quest events. | `GiveItem()` |
| `Assets/Scripts/GamePresentation/Inventory/InventoryModel.cs` | Grid-based inventory data, capacity preflight, and stack operations. | `GetSlot()`, `AddItem()`, `CanAddItem()`, `RemoveItem()`, `HasItem()`, `CountItem()`, `MoveSlot()`, `SplitStack()`, `Sort()`, `ForceRefresh()`, `OnChanged` |
| `Assets/Scripts/GamePresentation/Inventory/InventorySlot.cs` | Stores one item reference and quantity. | `IsEmpty`, `Set()`, `Clear()` |
| `Assets/Scripts/GamePresentation/Inventory/InventorySlotUI.cs` | Displays one inventory slot and handles drag, drop, hover, and clicks. | `Setup()`, `Refresh()`, pointer/drag/drop handlers |
| `Assets/Scripts/GamePresentation/Inventory/InventorySplitDialog.cs` | Lets the player select an exact stack-split quantity. | `Show()`, `Hide()`, `IsOpen` |
| `Assets/Scripts/GamePresentation/Inventory/InventoryTooltip.cs` | Displays item details beside the cursor. | `Show()`, `Hide()` |
| `Assets/Scripts/GamePresentation/Inventory/InventoryUI.cs` | Owns the player inventory model and slot grid. | `Instance`, `Model`, `IsOpen`, `InputLocked`, `Toggle()`, `Open()`, `Close()` |
| `Assets/Scripts/GamePresentation/Inventory/ItemData.cs` | ScriptableObject definition for an item. | Public item fields, `IsEquip`, `IsStackable` |
| `Assets/Scripts/GamePresentation/Inventory/ItemDatabase.cs` | Loads and registers item definitions by ID. | `Instance`, `Get()`, `TryGet()`, `Register()` |
| `Assets/Scripts/GamePresentation/Inventory/ItemType.cs` | Defines broad item categories. | Enum values |
| `Assets/Scripts/GamePresentation/Inventory/LootContainerUI.cs` | Displays and transfers items from a source inventory. | `Show()`, `Hide()`, `IsOpen` |
| `Assets/Scripts/GamePresentation/Inventory/LootSlotUI.cs` | Displays and handles interaction for one loot slot. | `Setup()`, `Refresh()`, pointer handlers |

## NPCs and Dialogue

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/NPCs/DialogueData.cs` | Serializable dialogue graph, node, and choice DTOs, including optional manual quest transition and quest-gating fields. | Public serialized fields |
| `Assets/Scripts/GamePresentation/NPCs/DialogueGate.cs` | Evaluates node/choice quest gating (`requireQuestId`, `requireQuestNodeId`). | `IsAvailable(requireQuestId, requireQuestNodeId)` |
| `Assets/Scripts/GamePresentation/NPCs/DialogueDatabase.cs` | Loads and indexes JSON/asset dialogue graphs. | `RegisterAsset()`, `TryGetDialogue()` |
| `Assets/Scripts/GamePresentation/NPCs/DialogueGraphAsset.cs` | ScriptableObject wrapper for a dialogue graph. | `Graph`, `DialogueId` |
| `Assets/Scripts/GamePresentation/NPCs/DialogueUIController.cs` | Displays the speaker, line, and choice list. | `GetOrCreate()`, `ShowDialogue()`, `HideDialogue()`, `IsShowingDialogue` |
| `Assets/Scripts/GamePresentation/NPCs/INpcBehavior.cs` | Contract for pluggable NPC behaviors. | `Weight`, `OnEnter()`, `Tick()`, `OnExit()`, `IsComplete()` |
| `Assets/Scripts/GamePresentation/NPCs/NpcBehaviorManager.cs` | Selects and runs `INpcBehavior` components. | Lifecycle-driven component |
| `Assets/Scripts/GamePresentation/NPCs/NpcController.cs` | NPC identity, type, state, movement lock, combat references, and optional inventory. | Identity/state properties, `CanInteract()`, `SetBehaviorState()`, `SetMovementEnabled()` |
| `Assets/Scripts/GamePresentation/NPCs/NpcDialogue.cs` | Connects an NPC to a dialogue graph and conversation state. | `CanStartDialogue()`, node lookup methods, `BeginConversation()`, `EndConversation()`, `SelectDialogue()` |
| `Assets/Scripts/GamePresentation/NPCs/NpcIdleBehavior.cs` | Waits for a randomized duration. | `INpcBehavior` implementation |
| `Assets/Scripts/GamePresentation/NPCs/NpcWanderBehavior.cs` | Walks toward randomized nearby destinations. | `INpcBehavior` implementation |

## Player

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/Player/PlayerController2D.cs` | Reads movement input and drives the player's `Rigidbody2D`. | `DisplayName`, `Stats`, `CombatReceiver`, `MovementEnabled`, `MoveSpeed`, `SetMovementEnabled()` |
| `Assets/Scripts/GamePresentation/Player/PlayerInteractionController.cs` | Finds nearby NPC/world interactables, drives conversations, and applies dialogue-selected manual quest transitions. | Input- and lifecycle-driven component |

## Portals

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/Portals/PortalManager.cs` | Coordinates component-authored same/cross-scene travel. | `Instance`, `TryUsePortal()`, `TryTeleportToPortal()`, `TryFindPortal()` |
| `Assets/Scripts/GamePresentation/Portals/PortalTrigger2D.cs` | Stores portal identity, destination, exit point, and trigger behavior. | `PortalId`, `DestinationScene`, `DestinationPortalId`, `ExitPoint`, `BlockForSeconds()` |
| `Assets/Scripts/GamePresentation/Portals/Editor/PortalMapExporter.cs` | Exports generated JSON/Markdown portal maps and validation results. | `ExportPortalMap()` |

## Quests and Persistent State

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/Quests/ICondition.cs` | Defines and implements quest-transition conditions. | `Evaluate()` and condition constructors |
| `Assets/Scripts/GamePresentation/Quests/IQuestAction.cs` | Defines and implements quest-node side effects. | `Execute()` and action constructors |
| `Assets/Scripts/GamePresentation/Quests/QuestData.cs` | Serializable quest graph DTOs. | Public serialized fields |
| `Assets/Scripts/GamePresentation/Quests/QuestEventBus.cs` | Broadcasts decoupled quest progress events. | `OnEvent`, `Raise()` |
| `Assets/Scripts/GamePresentation/Quests/QuestInstance.cs` | Tracks active quest nodes/objectives and separates automatic traversal from validated manual choices. | State properties, `OnEvent()`, `TryAdvance()`, `TryChooseTransition()` overloads |
| `Assets/Scripts/GamePresentation/Quests/QuestLoader.cs` | Loads quest JSON and builds condition/action instances. | `LoadAll()`, `Load()`, `BuildCondition()`, `BuildAction()` |
| `Assets/Scripts/GamePresentation/Quests/QuestManager.cs` | Owns active quests, manual transition routing, and quest save data. | `Instance`, `StartQuest()`, `TryChooseTransition()` overloads, query methods, `LoadSaveData()`, `GetSaveData()` |
| `Assets/Scripts/GamePresentation/Quests/WorldStateManager.cs` | Persistent singleton key/value store used by quests and world reactions. | Fact, flag, typed-value, snapshot APIs; `OnFlagChanged` |

## World Objects

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/World/EnemyLootPresenter.cs` | Seeds enemy loot and opens the loot panel after death. | Event-driven component |
| `Assets/Scripts/GamePresentation/World/IInteractable.cs` | Contract for player-interactable world objects. | `CanInteract()`, `GetDisplayName()`, `TryGetCurrentLine()`, `Advance()`, `EndInteraction()` |
| `Assets/Scripts/GamePresentation/World/ItemPickup.cs` | Transfers a placed item into inventory on player contact. | Trigger-driven component |
| `Assets/Scripts/GamePresentation/World/LootContainer.cs` | Interactable chest/container with an inventory model. | `IInteractable` implementation |
| `Assets/Scripts/GamePresentation/World/WorldObject.cs` | General-purpose line-based interactable object. | `IInteractable` implementation |

## World-State Reactions

| Script | Responsibility | Key runtime API |
|---|---|---|
| `Assets/Scripts/GamePresentation/WorldState/EnemyDeathFlagSetter.cs` | Sets a world-state flag when its enemy dies. | Event-driven component |
| `Assets/Scripts/GamePresentation/WorldState/WorldStateActivator.cs` | Activates/deactivates a target according to a flag. | Inspector-driven component |
| `Assets/Scripts/GamePresentation/WorldState/WorldStateDestroyer.cs` | Permanently destroys an object when a flag is set. | Inspector-driven component |
| `Assets/Scripts/GamePresentation/WorldState/WorldStateDialogueSelector.cs` | Selects NPC dialogue according to world state. | `Key` |
| `Assets/Scripts/GamePresentation/WorldState/WorldStateInteractable.cs` | Enables/disables guarded interactable components according to a flag. | Inspector-driven component |
| `Assets/Scripts/GamePresentation/WorldState/WorldStateKey.cs` | Designer-friendly ScriptableObject key asset. | `Key`, implicit string conversion |
| `Assets/Scripts/GamePresentation/WorldState/WorldStateNpcReactor.cs` | Moves or changes an NPC's state in response to a flag. | `Key` |
| `Assets/Scripts/GamePresentation/WorldState/WorldStateSpawner.cs` | Spawns a configured prefab according to a flag. | Inspector-driven component |
