# Economy Balancing Rules

## Priority

The most important principle in this document is the **soft economic reset**. A long simulation eventually lets the player solve its current economy. Progression must then introduce a new economic horizon with different constraints, demand, and investment targets.

A soft reset must not erase the player's wallet, inventory, relationships, knowledge, or accomplishments. It should make prior wealth helpful without allowing that wealth to instantly solve every new problem.

## 1. Soft Economic Resets

### Purpose

Once the player can reliably afford everything in the current tier, ordinary profit stops creating meaningful decisions. A new tier should reopen economic questions:

- What should I invest in first?
- Which resource is now scarce?
- Which relationship or market should I develop?
- How much mana can I safely commit while preserving spellcasting capacity?
- Which transformation gives me access to the next opportunity?

### Suitable reset moments

Use a soft reset when the player:

- enters a new settlement or market;
- gains access to a new NPC class or institution;
- chooses a specialization;
- discovers a new resource family;
- unlocks a new production tier;
- earns a guild rank or certification;
- begins operating at regional scale.

These moments should be delivered through the nested quest structure described in [QUEST_PROGRESSION_INTEGRATION.md](QUEST_PROGRESSION_INTEGRATION.md).

### Reset a constraint, not the save

Each new horizon should constrain one or more dimensions that previous wealth does not completely solve:

| Constraint | Example |
|---|---|
| Knowledge | The player needs a recipe, technique, or appraisal skill |
| Access | A guild, NPC, location, or market relationship must be earned |
| Inputs | The new transformation requires unfamiliar regional resources |
| Capacity | The player can afford an operation but cannot yet hold or channel enough mana |
| Throughput | Production time, tools, labor, or storage limit output |
| Logistics | Goods must reach another settlement or survive transport |
| Reputation | High-value participants will not trade without trust |
| Risk | Casting, production, or transport exposes the player to meaningful loss |
| Demand | A wealthy player still needs actual buyers with finite wallets and needs |

### Mana-specific rule

Because mana is both money and spell fuel, do not invalidate it through arbitrary inflation. Existing mana should provide flexibility, safety, and a faster start, but the new tier should also require knowledge, access, resources, capacity, or relationships.

Good mana sinks include:

- workshop and storage expansion;
- market stalls, permits, and certifications;
- route establishment;
- research and experimentation;
- capacity upgrades;
- large transformations;
- infrastructure and public projects;
- high-impact utility spells.

These sinks must purchase lasting capability or meaningful influence. They should not feel like a toll charged solely because the player became wealthy.

### Layered rather than total resets

Earlier production remains useful:

- common goods supply baseline consumption;
- earlier NPCs remain customers, suppliers, or workers;
- basic transformations feed advanced recipes;
- old locations retain dependable resources;
- established relationships reduce recovery pressure.

The new tier adds a different bottleneck instead of making everything old worthless.

### Quest delivery pattern

```text
Player demonstrates mastery of the current loop
└── a shortage, request, or consequence appears
    └── a connected NPC or institution makes contact
        └── a bridge quest introduces the new constraint
            └── the player receives one small foothold
                └── the new economic horizon opens
```

The foothold might be one recipe, one buyer, one permit, one resource sample, or temporary access to a facility. It lets the player begin learning before they can dominate the tier.

## 2. Early-Game Difficulty

The first 30–60 minutes should challenge the player without creating a likely unrecoverable state.

The intended experience is:

1. Learn the resource → transformation → trade/magic loop.
2. Make at least one meaningful “now or later” decision.
3. Earn a small, understandable win.
4. Clearly see the next connected objective.

Difficulty should come primarily from **decision pressure**, not crushing price pressure. Early choices might include:

- sell a mana-bearing resource now or refine it later;
- spend mana on a spell or preserve purchasing power;
- fill a known order or retain an ingredient;
- invest in capacity or buy a tool;
- help one NPC first and delay another opportunity.

Do not expose every recipe, customer class, market, or spell immediately. A small starting option set makes each unlock legible and gives nested quests meaningful rewards.

Early safeguards should provide recovery through play—labor, gathering, favors, low-risk orders, or non-magical solutions—without creating an unlimited passive mana faucet.

## 3. Bounded Randomness

Use the rule:

> Random in the short term; dependable in the long term.

RNG can vary individual outcomes, but repeated play should converge toward an understandable result.

Recommended tools:

- bounded outcome ranges;
- weighted distributions;
- guaranteed minimum yields;
- bad-luck protection;
- pity counters;
- curated demand pools;
- visible quality or risk modifiers;
- deterministic alternatives for essential progression resources.

An initial tuning target such as ±20% yield variance may be reasonable, but it is not a universal rule. Measure whether players can predict long-term production and whether a bad streak can block progression.

Never put an essential quest resource, phase transition, or recovery path behind unbounded RNG.

NPC demand should be constrained by profession, class, location, season, inventory, and actual needs. Curated randomness creates variety; unrestricted randomness makes the market feel incoherent.

## 4. Economy Spreadsheet

Maintain a balancing workbook before the content catalog becomes large.

At minimum, each item or transformation row should include:

| Field | Purpose |
|---|---|
| Stable ID | Matches the runtime item or recipe ID |
| Phase and unlock time | Shows when the opportunity enters play |
| Inputs and quantities | Defines resource cost |
| Effective input cost | Includes purchase or opportunity cost |
| Production time | Enables throughput calculations |
| Mana cost | Captures direct economic and spellcasting tradeoff |
| Expected yield | Uses the mean of any bounded RNG |
| Guaranteed minimum yield | Reveals bad-luck viability |
| Sale price range | Captures market variation |
| Expected profit | Revenue minus all effective costs |
| Profit per minute | Compares transformations with different durations |
| Capacity requirement | Shows mana/storage/channel bottlenecks |
| Required access | Recipe, reputation, NPC, location, tool, or rank |
| Primary buyer class | Connects output to real demand |
| Intended role | Recovery, staple, specialization, luxury, quest, or prestige |

Plot several curves rather than only “maximum money at hour X”:

- expected active mana earnings;
- conservative earnings under poor outcomes;
- unavoidable and optional mana sinks;
- mana capacity;
- spell expenditure;
- production throughput;
- wealth held by NPC market participants;
- time required to afford the next meaningful investment.

The target does not need to be mathematically logarithmic everywhere. The important shape is rapid early understanding and growth, followed by diminishing returns within a tier, then a new decision curve when a soft reset opens another horizon.

## 5. Gifts and Rewards

Uncontextualized free mana weakens scarcity. Gifts are useful when they carry narrative or systemic meaning:

- a quest reward;
- a tip following exceptional service;
- a favor from a trusted NPC;
- recovered treasure;
- compensation for risk;
- an emergency loan with consequences;
- a rare resource or recipe instead of liquid mana.

A starting playtest target is to keep an ordinary gift near 10–20% of expected active hourly earnings. Treat that range as a tuning hypothesis, not a permanent rule.

Evaluate a gift by asking:

1. Why did this character or system provide it?
2. What decision does it enable?
3. Does it bypass a meaningful unlock or soft reset?
4. Does the giver actually transfer the mana or item from an owned supply?
5. Will repeated gifts become an exploitable faucet?

Rare seeds, recipes, access, information, capacity, and relationships can be more memorable than a larger mana payment because they open new decisions.

## 6. Relationship to NPC Markets

Soft resets must respect the planned player/NPC market:

- NPCs retain real wallets and inventories.
- New buyer classes have bounded demand rather than infinite purchasing power.
- New regions can have different shortages without inventing goods from nothing.
- Earlier suppliers can feed later production chains.
- Major gifts and quest rewards should identify a source where practical.
- Market shocks should create opportunities and consequences, not random price noise.

An economic horizon is successful when it changes what players and NPCs want, produce, transport, and consume—not merely when every price receives another zero.

## 7. Playtest Questions

For each phase and soft reset, record:

1. When did the player first understand the core loop?
2. What was their first meaningful “now or later” choice?
3. Could a bad RNG streak block recovery or progression?
4. At what point could the player afford everything relevant?
5. What new constraint reopened decision-making?
6. Did old wealth help without instantly solving the new tier?
7. Did older resources, NPCs, and transformations remain useful?
8. Did mana rewards feel earned and narratively grounded?
9. Did the player preserve mana for spells, or did one use dominate?
10. How long did the next meaningful investment take to reach?

## Implementation Order

1. Define the first 30–60 minute decision loop.
2. Build the economy workbook for the early item and transformation set.
3. Add bounded RNG and guaranteed recovery assumptions to the workbook.
4. Playtest until the first win and next objective are consistently understood.
5. Design the first early-to-mid soft reset.
6. Deliver it through one bridge quest and one new economic constraint.
7. Measure whether established wealth helps without trivializing the new tier.
8. Repeat this pattern before designing the full late-game catalog.
