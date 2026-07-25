# Quest-Driven Progression and Nested Unlocks

## Verdict

The current graph-based quest system is a strong foundation for nested progression. Quests should act as the **orchestration layer**: they observe ordinary play, advance through authored nodes, and write stable world-state facts. NPC, location, market, recipe, and other gameplay systems should react to those facts independently.

This supports the intended progression pattern without arbitrary level gates or mana paywalls:

```text
Familiar activity
└── introduces an NPC
    └── starts a quest
        └── reveals a resource or problem
            └── leads to a location
                └── teaches a transformation
                    └── opens a market or system
```

## How Existing Quest Features Map to the Progression

| Progression step | Existing mechanism |
|---|---|
| Notice familiar activity | A quest objective listens for a `QuestEventBus` event such as `ItemCollected`, `NpcTalkedTo`, or a future `TradeCompleted` event |
| Introduce an NPC | `SetFact` enables or spawns the NPC through `WorldStateActivator` or `WorldStateSpawner` |
| Change what an NPC says | `SetFact` selects new dialogue through `WorldStateDialogueSelector` |
| Begin a connected quest | `StartQuest` activates another quest graph |
| Reveal a location | `SetFact` enables a portal, entrance, map interaction, or location object |
| Reveal a resource | `SetFact` enables a resource node, supplier, or drop source |
| Unlock a transformation | `SetFact` records access; the future recipe/transformation system reads that fact |
| Unlock a market or system | `SetFact` records access; the market or system reacts to it |
| Branch based on prior play | `Fact`, `HasItem`, `QuestInNode`, and objective conditions select graph transitions |

The important boundary is that quest actions should not directly manipulate scene objects. A quest writes facts such as `Unlock.Npc.ManaRefiner`; the relevant world component decides how that fact changes the scene.

## Example Early-to-Mid Unlock Chain

1. The player sells a common mana-bearing herb during ordinary early-game play.
2. A hidden quest objective receives `TradeCompleted/herb_common`.
3. The quest sets `Unlock.Npc.ManaRefiner`.
4. The refiner appears and offers `refiners_request`.
5. That request introduces an unstable crystal found in a nearby marsh.
6. Reaching the marsh sets or satisfies a location objective.
7. Returning the crystal sets `Unlock.Transformation.RefineCrystal`.
8. Using the transformation once sets up the next quest and unlocks `Unlock.Market.RefinedMana`.

The player reaches the new system by following relationships between familiar actions. No step requires “reach level 10” or “pay 1,000 mana to continue.”

An unlock node can use the existing actions:

```json
{
  "id": "refiner_introduced",
  "objectives": [],
  "transitions": [],
  "onEnterActions": [
    {
      "type": "SetFact",
      "key": "Unlock.Npc.ManaRefiner",
      "value": "True"
    },
    {
      "type": "StartQuest",
      "questId": "refiners_request"
    }
  ]
}
```

## Recommended World-State Naming

Use stable, content-facing IDs rather than display names:

```text
Unlock.Npc.ManaRefiner
Unlock.Resource.UnstableCrystal
Unlock.Location.EastMarsh
Unlock.Transformation.RefineCrystal
Unlock.Market.RefinedMana
Unlock.System.Reputation

Quest.RefinersRequest.Started
Quest.RefinersRequest.Completed
Progression.Early.CoreLoopUnderstood
Progression.Mid.FirstSpecialization
```

The `Unlock.*` facts describe capabilities in the world. `Quest.*` facts preserve narrative outcomes. `Progression.*` facts summarize broader milestones only when another system needs them.

Do not make a single broad fact such as `MidGameUnlocked` responsible for everything. Individual facts keep branches independent and make save data easier to understand.

## Important Current Runtime Gap

`QuestTransitionData` contains an `automatic` field, but `QuestInstance.TryAdvance()` currently does not inspect it. Every transition advances immediately when its conditions pass, including transitions authored with `"automatic": false`.

Before authoring dialogue choices or player-confirmed branches, either:

1. implement a manual transition API such as `TryChooseTransition(targetNodeId)`, while `TryAdvance()` processes only automatic transitions; or
2. remove the field and deliberately model every transition as automatic.

The first option fits this project better because introductions, bargains, refusals, and specialization choices should wait for an explicit player decision.

## Minimal Extensions Needed

Build these only as their connected gameplay systems arrive:

1. **Trade quest events** — raise `TradeCompleted` after a successful atomic trade, with stable item, trader, or market IDs.
2. **Manual quest choices** — honor the `automatic` field and expose a validated choice-transition method for dialogue.
3. **Quest completion state** — expose a durable completed/failed status or set explicit `Quest.<Id>.Completed` facts at terminal nodes.
4. **Mana conditions and actions** — after Wallet and MP are unified, add checks and transfers that use the canonical mana account.
5. **Reputation changes** — add an integer increment/decrement action rather than overwriting reputation with `SetFact`.
6. **Shared item lookup** — resolve quest items through `ItemDatabase`; current `HasItem`, `GiveItem`, and `RemoveItem` use `Resources.Load`.
7. **Content validation** — validate node IDs, target nodes, objective IDs, action types, condition types, quest IDs, item IDs, and duplicate IDs before play.

Facts are already sufficient for NPC, resource, location, and future transformation/system unlocks. Separate action types such as `UnlockLocation` are unnecessary unless they need behavior beyond writing a fact.

## Phase Transitions

Treat each phase transition as a short bridge quest rather than a threshold:

- **Early to mid:** a familiar trade exposes a shortage, introduces a specialist, and opens the first specialization.
- **Mid to late:** the consequences of the player's specialization create a regional request, institution, or crisis that requires influence rather than simple profit.

Use multiple evidence signals when deciding whether a bridge quest can begin:

- a relevant familiar action has occurred;
- the player has demonstrated one transformation;
- a relationship or quest outcome supports the introduction;
- the new system has a narrative reason to appear;
- the player has a recovery path if their mana reserve is low.

Mana may be part of a solution, but it should not be the sole admission price.

When the current tier has been economically solved, the bridge quest should introduce a soft reset by changing the relevant constraint rather than erasing progress. See [ECONOMY_BALANCING_RULES.md](ECONOMY_BALANCING_RULES.md).

## Authoring Guardrails

- Every unlock should answer: “Which familiar action caused the player to discover this?”
- New systems should first appear in a small, low-risk use case.
- Branch failures should redirect the player or change the opportunity, not silently dead-end progression.
- `onEnterActions` run once when a node is first entered and are intentionally not replayed when loading a save.
- Use stable IDs in events and facts; keep localized display text outside those IDs.
- Prefer facts over direct references between quests and scene objects.
- Avoid polling for progression in unrelated systems; let them react to `WorldStateManager` changes.
- Test each bridge quest from a low-mana save as well as a well-funded save.

## Recommended First Content Slice

Implement one complete early-game chain before designing every phase:

1. One common resource.
2. One basic transformation.
3. One buyer and one ordinary sale.
4. One `TradeCompleted` quest event.
5. One specialist NPC unlocked by that activity.
6. One short follow-up quest.
7. One new resource location.
8. One new transformation or market access reward.

This slice exercises quests, world state, trade, mana, NPC introduction, location access, and transformation progression together. It will reveal whether the nested structure feels natural before the content graph becomes large.
