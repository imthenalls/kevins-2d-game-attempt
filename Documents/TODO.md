# Development Roadmap

Unchecked items are planned work. Completed work is retained below as a concise project-history checklist.

## Next

- [ ] **Define canonical mana currency** — choose its final name, capacity rules, emergency reserve behavior, and permitted faucets/sinks.
- [ ] **Mana transaction categories** — distinguish trade, spell, crafting, reward, extraction, fee, and loss records.
- [ ] **Mana scarcity safeguards** — remove free-money regeneration loops and provide non-softlocking recovery paths for players and NPCs.
- [ ] **Early-game economic arc** — design and playtest the first 0–3 hours around learning, stability, local trade, basic spells, and meaningful small rewards.
- [ ] **Mid-game specialization plan** — define the first new profession/production branch, NPC class, location, reputation gate, and active market expansion.
- [ ] **Late-game prestige plan** — define influence, infrastructure, rare transformations, institutional rank, and world-changing rewards beyond simple mana accumulation.
- [ ] **Nested progression unlocks** — introduce systems through connected tasks, NPCs, quests, resources, and locations instead of hard level or currency walls.
- [ ] **First soft economic reset** — design the early-to-mid bridge so prior wealth helps but new knowledge, access, inputs, capacity, reputation, logistics, or demand prevents instant completion.
- [ ] **Economy balancing workbook** — track item inputs, time, mana cost, expected/minimum yield, prices, profit, throughput, unlock timing, capacity, access, and buyer class.
- [ ] **Bounded economy RNG** — define yield/demand ranges, guaranteed minimums, curated pools, and bad-luck protection; never gate essential progression behind unbounded randomness.
- [ ] **Narrative reward budget** — measure active hourly earnings and test ordinary mana gifts near the provisional 10–20% range while preventing repeatable faucets.
- [ ] **Manual quest transitions** — make `QuestInstance.TryAdvance()` honor `QuestTransitionData.automatic` and add a validated player/dialogue choice transition API.
- [ ] **Trade quest events** — raise `TradeCompleted` only after an atomic trade succeeds, using stable item, trader, and market IDs for quest objectives.
- [ ] **Quest progression status** — persist explicit completed/failed quest state or consistently write terminal `Quest.<Id>.Completed` facts.
- [ ] **Quest economy extensions** — after Wallet/MP unification, add canonical mana conditions/actions and integer reputation adjustments.
- [ ] **Quest item resolution** — update `HasItem`, `GiveItem`, and `RemoveItem` to resolve stable IDs through `ItemDatabase` instead of direct `Resources.Load`.
- [ ] **Quest content validation** — validate quest/node/objective/action/condition/item IDs and transition targets before entering play mode.
- [ ] **Currency HUD** — display `Wallet.Balance` and update it through `Wallet.OnBalanceChanged`.
- [ ] **Shared atomic trade service** — support player-to-NPC and NPC-to-NPC buying/selling through one validated transaction path with no partial state changes.
- [ ] **Trade participants** — add stable trader IDs and a shared participant contract exposing a Wallet and InventoryModel.
- [ ] **Market ledger** — save buyer, seller, item, quantity, price, market, and shared trade ID; correlate both wallet entries to the trade.
- [ ] **NPC wallet persistence** — include every economic NPC's wallet and retained history in save data.
- [ ] **NPC market simulation** — schedule producer, consumer, stock-target, offer-matching, and trading decisions on bounded market ticks.
- [ ] **Dynamic market pricing** — adjust prices from supply, demand, stock targets, and local modifiers with safeguards against runaway inflation/deflation.
- [ ] **Currency rewards** — add quest and loot integrations that call `Wallet.Add` with stable reason/reference IDs.
- [ ] **Transaction viewer** — provide a development/debug UI for the wallet's saved transaction history.
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
- [x] **Wallet foundation** — non-negative balance, affordability checks, add/spend/subtract operations, events, bounded transaction history, and save/load integration
- [x] **Unified mana account** — `Wallet` now owns player mana balance/capacity, `EntityStats` delegates its MP API, equipment changes capacity, and older saves merge wallet currency with MP.

### Presentation and behavior

- [x] **NPC wander sprite flipping** — `NpcWanderBehavior` updates `SpriteRenderer.flipX` from horizontal movement
