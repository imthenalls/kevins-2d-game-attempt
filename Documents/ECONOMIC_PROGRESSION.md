# Economic Progression: Early, Mid, and Late Game

## Design Principle

A long simulation game should not run the same economy with larger numbers for its entire duration. Progression should move through three distinct economic phases:

- **Early game:** learn and stabilize
- **Mid game:** expand and specialize
- **Late game:** master and influence

Each phase needs:

- A different economic dynamic
- A clear player goal
- New uses for mana
- New rewards
- New decisions rather than only larger costs

The intended pacing targets are guidelines, not hard gates:

| Phase | Approximate time | Primary player motivation |
|---|---:|---|
| Early | 0–3 hours | Learn the loop and become stable |
| Mid | 3–15 hours | Expand, specialize, and optimize |
| Late | 15+ hours | Master systems, pursue prestige, and influence the wider economy |

## Early Game: Learn and Stabilize

### Player experience

The player is learning:

- Mana is both currency and spell fuel.
- Casting has an economic opportunity cost.
- Items and labor can be transformed into mana.
- NPCs own real inventories and mana reserves.
- Trade transfers resources rather than creating them.

Absolute values should remain small so every transaction is legible. A reward of 10 mana should feel significant because the player understands exactly what it can buy or cast.

### Early-game goals

- Secure a reliable but limited mana source.
- Learn one or two basic production transformations.
- Complete simple local trades.
- Cast useful low-cost spells.
- Build an emergency reserve.
- Establish relationships with nearby NPCs.

### Appropriate systems

- Basic wallet and mana-capacity UI
- Simple gathering or extraction
- Common goods
- A small local market
- A few understandable spells
- Direct NPC requests
- Introductory crafting
- Clear transaction feedback

### Reward structure

Early rewards should be immediately useful:

- Enough mana for a meaningful spell
- A capacity increase
- A tool that improves one transformation
- Access to a dependable buyer or seller
- A recipe with an obvious use
- Information about a nearby resource source

Avoid early rewards that are technically valuable but too abstract for the player to evaluate.

### Failure protection

Because mana powers both trade and magic, the player must not be able to permanently trap themselves at zero.

Early recovery options may include:

- Low-risk labor
- A small non-transferable emergency reserve
- Limited gathering
- Favors or credit from trusted NPCs
- Non-magical solutions to early problems

Recovery should require action, but it should not create an unlimited passive mana faucet.

## Mid Game: Expand and Specialize

### Player experience

The player understands the basic loop and begins asking:

- Which production chain is most efficient?
- Which spells are worth their economic cost?
- Which NPCs and settlements should receive scarce goods?
- Should mana be invested in capacity, production, defense, or inventory?
- Which market shortages can be exploited or solved?

The economy should become interconnected. NPC-to-NPC trading becomes visible and begins changing stock and prices.

### Mid-game goals

- Choose professions, production chains, or magical specializations.
- Increase mana storage and throughput.
- Enter additional markets or settlements.
- Build relationships and reputation with economic groups.
- Automate or delegate basic work.
- Respond to shortages and regional demand.

### Appropriate systems

- Shared atomic trade service
- NPC market simulation
- Multiple trader classes or professions
- Production and consumption profiles
- Supply/demand price modifiers
- Market or settlement boundaries
- Reputation and access rules
- More complex crafting
- Specialized spell schools
- Storage and transport

### Reward structure

Mid-game rewards should expand possibilities:

- New transformations and recipes
- Access to specialized NPCs
- New locations or markets
- Increased mana capacity
- Production upgrades
- Better storage or transport
- Reputation privileges
- Spells that alter production, travel, information, or trade

Rewards should create new strategies instead of making the existing strategy uniformly stronger.

## Late Game: Master and Influence

### Player experience

By late game, the player will probably have solved or broken the basic personal economy. Ordinary profit and survival are no longer sufficient goals.

The focus should shift from accumulating mana to deciding how mana moves through the world.

### Late-game goals

- Influence settlement or regional markets.
- Complete legendary transformations.
- Establish institutions, guilds, routes, or production networks.
- Manage large mana reserves and large-scale sinks.
- Pursue prestige, collection, status, and rare knowledge.
- Stabilize or deliberately disrupt economies.
- Decide who benefits from scarce resources.

### Appropriate systems

- Guild or institutional progression
- Regional trade routes
- Large commissions
- Rare resources
- Legendary spells and recipes
- Political or faction access
- Market policy
- Taxes, public projects, and infrastructure
- Large-scale crises and shortages
- Prestige collections and accomplishments

### Reward structure

Late rewards should emphasize:

- Unique capabilities
- Recognition and rank
- World changes
- Rare collection pieces
- New rules or exceptions
- Control over infrastructure
- Access to legendary transformations
- The ability to shape NPC and settlement behavior

Simply awarding more mana is weak late-game motivation. Mana should become a tool for achieving influence and prestige.

## Smooth Phase Transitions

Do not transition phases through a single hard level requirement or an arbitrary mana paywall.

Use nested objectives:

```text
Complete a familiar task
└── Meet a new NPC through that task
    └── Receive a request involving a new resource
        └── Travel to a new location
            └── Learn a new transformation
                └── Gain access to a new market or system
```

Good transitions arise from the player's existing activity:

- Selling a common good introduces a specialist buyer.
- Casting a practical spell attracts a teacher or faction.
- A local shortage reveals a regional supply chain.
- Helping an NPC unlocks a market relationship.
- A capacity upgrade makes a previously impossible transformation viable.
- A quest outcome changes production or consumption in a settlement.

The player should understand why the new system appeared and how it connects to what they were already doing.

## Mana Economy Across the Phases

| Economic dimension | Early | Mid | Late |
|---|---|---|---|
| Mana scale | Small, readable amounts | Larger flows and reserves | Institutional or regional flows |
| Primary use | Survival, basic spells, local purchases | Investment, specialization, production | Influence, infrastructure, legendary magic |
| Trade | Direct local exchanges | Active NPC markets and regional differences | Networks, routes, policy, major commissions |
| Scarcity | Personal and immediate | Supply-chain and profession-specific | Regional, political, and systemic |
| Rewards | Immediately useful tools and mana | New options and efficiencies | Prestige, control, unique capabilities |
| Player question | “Can I afford this?” | “What should I specialize in?” | “What kind of world economy do I want?” |

## Progression Metrics

Track progression using several signals rather than only playtime:

- Number of known transformations
- Number of active market relationships
- Mana capacity
- Sustainable mana income and consumption
- Access to locations
- Reputation or institutional rank
- Production throughput
- Variety of goods traded
- Spell schools or utility categories unlocked
- Influence over NPCs, markets, and settlements

Playtime estimates help pacing, but phase transitions should respond to demonstrated mastery and connected objectives.

## Balancing Questions

For every phase, answer:

1. What is the player's main economic problem?
2. What new decision appears in this phase?
3. What new use for mana becomes available?
4. What resource becomes scarce?
5. What transformation becomes possible?
6. What reward changes strategy?
7. What existing activity naturally introduces the next phase?
8. What prevents the optimal strategy from remaining unchanged for the entire game?

## Implementation Guidance

Build the early-game economy first and measure it before implementing the full late game.

Recommended order:

1. Define a 30-minute core loop.
2. Extend it into a complete 0–3 hour early-game arc.
3. Confirm that mana decisions are understandable and recoverable.
4. Add one mid-game specialization and one new market.
5. Verify that the specialization changes decisions.
6. Add additional mid-game branches.
7. Design late-game influence systems only after the base economy can be sustainably “solved.”

Every new system should identify which phase it serves. A feature that serves no phase or does not create a new decision should be reconsidered.

## Quest Orchestration

The existing quest graph and world-state systems should deliver these phase transitions. Quests observe familiar activity, set stable unlock facts, introduce connected quests, and let world components reveal the corresponding NPCs, resources, locations, transformations, and systems.

See [QUEST_PROGRESSION_INTEGRATION.md](QUEST_PROGRESSION_INTEGRATION.md) for the concrete mapping, authoring guardrails, manual decision flow, and recommended first content slice.

## Balancing and Soft Resets

Each phase should eventually reach diminishing returns, then open a new economic horizon with a different constraint. This is a soft reset: prior wealth and accomplishments remain useful, but access, knowledge, inputs, capacity, logistics, reputation, or demand prevent accumulated mana from instantly solving the new tier.

See [ECONOMY_BALANCING_RULES.md](ECONOMY_BALANCING_RULES.md) for soft-reset design, early difficulty, bounded randomness, economy workbook fields, gifting rules, and playtest questions.
