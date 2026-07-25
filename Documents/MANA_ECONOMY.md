# Mana as Currency and Spell Fuel

## Core Idea

The main currency is mana: the same scarce resource used to purchase goods and cast spells.

This gives the currency intrinsic value. A unit of mana is not valuable only because vendors accept it; it can directly produce useful effects in the world. Every economic choice therefore has an immediate gameplay opportunity cost:

- Buying equipment leaves less mana for spells.
- Casting powerful magic consumes wealth that could have purchased goods.
- Selling goods increases both purchasing power and magical capability.
- NPC demand comes from actual consumption, not arbitrary shop behavior.

## Recommended Model

Use one canonical mana balance for both trading and spell costs.

```text
Mana balance
├── Transfer to another actor during trade
├── Consume to cast spells
├── Consume in crafting or industry
├── Gain through extraction, rewards, or production
└── Lose through fees, decay, waste, or other sinks
```

Do not maintain one spendable “gold mana” pool and a separate freely regenerating spell-mana pool unless conversion between them is the intentional design. Two unrelated pools would weaken the central economic tradeoff.

## Current Repository State

The player now has one canonical mana account:

- `Wallet.Balance` is both purchasing power and castable mana.
- `Wallet.Capacity` is the storage/channel limit.
- `EntityStats.Mp` and `EntityStats.MaxMp` delegate to the Wallet.
- `EntityStats.SpendMp`, `RestoreMp`, and `SetMp` remain compatibility APIs.
- PlayerController2D adds and binds a Wallet when an existing scene lacks one.
- Enemies without Wallet components retain local MP behavior.

## Resources, Transformations, and Scarcity

### Resources

The initial economy can use:

- Mana
- Items and raw materials
- NPC labor/time
- Production capacity
- Storage or carrying capacity

Mana is both:

- A medium of exchange
- A consumable input into magic and production

### Transformations

Every system should describe where resources come from and where they go.

| Transformation | Inputs | Outputs | Economic role |
|---|---|---|---|
| Mana extraction | Time, location, tools, depleted source | Mana | Faucet |
| Spellcasting | Mana | Damage, healing, movement, information, etc. | Sink |
| Crafting | Materials, mana, time | Items | Conversion |
| Trade | Buyer's mana, seller's item | Seller's mana, buyer's item | Transfer |
| Item consumption | Item | Healing, buffs, production | Sink/conversion |
| NPC production | Inputs, mana, labor/time | Goods | Conversion |
| Taxes/fees | Mana | Public service or removal | Transfer/sink |

Normal trade must not create or destroy mana. The buyer loses exactly what the seller receives.

### Scarcity

Mana remains valuable only when creation is constrained.

Possible constraints:

- Mana deposits deplete and recover slowly.
- Extraction requires tools, territory, labor, or danger.
- NPCs consume mana for work, travel, defense, and magic.
- Storage has a capacity or maintenance cost.
- Some transformations lose a percentage as waste.
- Powerful spells have meaningful mana costs.
- Mana generation is slower than the demand created by all useful activities.

## Critical Rule: No Free Regeneration

If spell mana regenerates freely and the same mana is currency, free regeneration becomes unlimited money.

Avoid:

- Full mana restoration by waiting with no cost
- Free inn restoration
- Cheap potions that restore more mana value than they cost
- Repeatable zero-risk actions that generate mana indefinitely

Safe alternatives:

- Regeneration draws from an owned stored reserve.
- Resting consumes food, fuel, time, or a lodging fee.
- Natural regeneration stops at a small emergency floor that cannot be traded.
- Mana returns slowly from limited world sources.
- Potions convert ingredients into mana and cost at least their recoverable value.

If an emergency floor is used, mark it as non-transferable and exclude it from the market supply.

## Capacity

A single mana pool raises a design question: can actors store unlimited mana?

Recommended starting rule:

- Every actor has a mana capacity.
- Equipment, containers, buildings, or upgrades can increase capacity.
- A trade cannot transfer mana beyond the recipient's available capacity.
- Excess rewards must remain unclaimed, spill into a physical mana item, or be routed to storage.

This makes storage infrastructure economically useful and preserves `MaxMp`-style progression.

## Spellcasting

Spell systems should spend through the same accounting API used by other mana sinks.

Conceptually:

```csharp
bool cast = mana.TryConsumeMana(
    amount: spell.manaCost,
    reason: "Cast spell",
    referenceId: spell.spellId);
```

A spell must not apply its effect unless the mana debit succeeds.

Spell consumption should have a distinct transaction category from market spending so analytics and history can separate:

- Trade
- Spell
- Crafting
- Fee
- Loss
- Reward
- Extraction

`WalletTransactionType` distinguishes credits, purchases, non-purchase subtraction, spells, restoration, capacity adjustments, direct adjustments, and save migration. Trade, crafting, reward, and extraction categories can be added alongside their systems.

## NPC Behavior

NPCs should value mana based on expected future utility.

Examples:

- A mage reserves mana for spells and buys less aggressively.
- A vulnerable settlement holds defensive reserves.
- A producer spends mana only when expected output value exceeds the cost.
- A desperate NPC may sell goods below normal price to obtain survival mana.
- Wealthy NPCs can consume more magic or invest in production.

Every trader should have a minimum mana reserve. Market AI must not spend below that reserve unless its behavior explicitly allows emergency liquidation.

## Recommended Refactor

### Phase 1: Define the canonical resource

- Choose the final player-facing name for mana currency.
- Decide whether the balance has a maximum capacity.
- Decide whether any emergency/non-transferable reserve exists.
- Define the permitted mana faucets and sinks.

### Phase 2: Unify the code — complete

- `Wallet` is the canonical mana account.
- Spell/MP costs route through the Wallet for bound entities.
- `EntityStats` preserves its public MP API as a compatibility facade.
- Equipment MP bonuses change Wallet capacity.
- Spell, restoration, adjustment, and migration transactions are categorized.
- Older saves combine their wallet balance and player MP without losing either value.

Trade, crafting, reward, extraction, and fee categories should be introduced with those systems so their atomic behavior and identifiers are defined together.

### Phase 3: Connect the market

- Use mana for all player and NPC trades.
- Add minimum reserves to trader behavior.
- Enforce recipient capacity during transfers.
- Correlate both sides of every trade in the market ledger.

### Phase 4: Balance scarcity

- Measure mana creation and destruction per game day.
- Measure mana concentration across actors and settlements.
- Detect actors stuck at zero.
- Detect runaway inflation or permanent deflation.
- Tune production, spell costs, and sinks from simulation results.

## Failure States to Design Around

- Player spends all mana and cannot cast a spell required to progress.
- Poor NPCs lose the ability to work, defend themselves, or re-enter the market.
- Rich actors accumulate mana permanently and freeze circulation.
- Free regeneration causes infinite purchasing power.
- Spell use destroys mana faster than extraction can replace it.
- Storage caps make quest rewards disappear.
- Market prices collapse because NPCs do not consume goods or mana.

Provide recovery paths such as low-risk work, emergency extraction, loans, favors, or non-magical solutions. Recovery should require gameplay, not generate unlimited mana passively.

## Economy Invariants

- Trade transfers mana but does not create or destroy it.
- Spells and defined sinks destroy mana.
- Only defined faucets create mana.
- No balance becomes negative.
- No transfer exceeds recipient capacity.
- Every mana change has a category, reason, and stable reference ID.
- Failed spells and trades change no balances.
- The player always has at least one non-softlocking recovery path.

## Long-Form Progression

Mana should change roles across the campaign:

- Early game: personal survival, learning, basic spells, and local trade
- Mid game: investment, specialization, production, and active NPC markets
- Late game: infrastructure, prestige, regional influence, and legendary magic

See [ECONOMIC_PROGRESSION.md](ECONOMIC_PROGRESSION.md) for phase goals, rewards, and smooth nested unlocks.
