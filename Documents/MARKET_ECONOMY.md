# Simulated Market Economy

## Design Goal

The economy must support NPCs buying and selling from each other, not only player-to-vendor shopping. Player trades, NPC trades, quest purchases, and scripted trades should all use the same transaction service so mana and items cannot be duplicated or lost.

The primary currency is mana, which is also consumed by spellcasting and potentially crafting or production. See [MANA_ECONOMY.md](MANA_ECONOMY.md) for the shared-resource and scarcity rules.

The market should feel active even when the player is not directly involved:

- NPCs own wallets and inventories.
- NPCs produce, consume, buy, and sell goods.
- Prices can react to supply, demand, stock targets, and local conditions.
- Every completed trade transfers real items and real mana between participants.
- Trade and wallet histories remain auditable after saving and loading.

## Core Rule

Do not implement separate “player buys” and “NPC buys” code paths.

All trades should eventually go through one API:

```csharp
TradeResult result = TradeService.TryExecute(request);
```

Both participants use the same underlying model:

```text
Trade Participant
├── Stable participant ID
├── Wallet
├── InventoryModel
└── Trading rules / preferences
```

The player is simply one trade participant whose decisions come from UI. NPC decisions come from market simulation logic.

## Proposed Components

### `ITradeParticipant`

Exposes the minimum state required for trading:

```csharp
string TradeParticipantId { get; }
Wallet Wallet { get; }
InventoryModel Inventory { get; }
```

Do not use display names as IDs. Player, NPC, settlement, and organization traders need stable save-safe identifiers.

### `Trader`

A component placed on economic NPCs or other trading entities. It should provide:

- Stable participant ID
- Wallet reference
- Inventory reference
- Items the trader may buy or sell
- Desired stock levels
- Minimum reserve money
- Pricing preferences
- Optional producer/consumer behavior

`NpcController` can own or expose a `Trader`, but trading logic should not be embedded directly into NPC movement/dialogue code.

### `TradeRequest`

Contains the complete proposed exchange:

- Buyer
- Seller
- Item ID
- Quantity
- Unit price
- Total price
- Reason/source
- Optional market or settlement ID

The total should be calculated with overflow-safe integer arithmetic.

### `TradeService`

The single authority for item-for-money exchanges.

Before changing state it validates:

1. Buyer and seller are different valid participants.
2. Quantity and price are positive.
3. Seller owns enough stock.
4. Buyer has enough inventory capacity.
5. Buyer wallet can afford the total.
6. Seller is permitted to sell the item.
7. Buyer is permitted to buy the item.

Only after every check succeeds should it commit:

1. Remove items from the seller.
2. Add items to the buyer.
3. Debit the buyer wallet.
4. Credit the seller wallet.
5. Write one market transaction.
6. Raise one trade-completed event.

No failure may leave only part of the exchange applied. Inventory capacity must therefore be checkable before mutation. `InventoryModel` will need a non-mutating `CanAddItem(item, quantity)` or equivalent reservation/preflight API.

### `MarketTransaction`

Wallet records explain balance changes, but a market transaction must describe the entire exchange.

Store:

- Unique trade ID
- UTC timestamp or simulation time
- Buyer participant ID
- Seller participant ID
- Item ID
- Quantity
- Unit price
- Total price
- Market/settlement ID
- Trade reason or source

The buyer's `Spend` record and seller's `Credit` record should both use the trade ID as their shared reference/correlation ID. This makes the two accounting entries traceable to the same market exchange.

### `MarketLedger`

Owns a bounded history of completed `MarketTransaction` records and includes it in save data.

The market ledger is distinct from wallet history:

- Wallet history answers: “Why did this balance change?”
- Market history answers: “Who traded which item with whom, where, and at what price?”

## Pricing Model

Start with deterministic pricing before adding complex simulation:

```text
price = basePrice × supplyMultiplier × demandMultiplier × localModifier
```

Recommended safeguards:

- Minimum and maximum price multipliers
- Integer rounding rules
- No negative or zero prices
- Stock targets rather than raw inventory counts
- Slow price changes to avoid oscillation
- Explicit mana faucets and sinks

Examples of faucets:

- Quest rewards
- Newly minted settlement income
- Starting NPC wealth

Examples of sinks:

- Taxes
- Repair costs
- Travel fees
- Item consumption or destruction

Normal NPC-to-NPC trades must conserve mana: the buyer loses exactly what the seller gains. Spellcasting and other explicit sinks may destroy mana; extraction and other explicit faucets may create it.

## NPC Decision Model

Do not simulate every NPC every frame.

Use scheduled market ticks:

1. Producers add limited goods according to elapsed simulation time.
2. Consumers evaluate needs and desired stock.
3. Traders publish offers or query available sellers.
4. A matcher selects affordable, permitted trades.
5. `TradeService` executes each accepted trade.

Begin with a small set of deterministic rules:

- Buy when stock is below a desired minimum.
- Sell when stock exceeds a desired maximum.
- Maintain a minimum wallet reserve.
- Prefer lower prices within a travel/market boundary.
- Limit transactions per market tick.

More advanced behavior such as professions, relationships, scarcity memory, speculation, and transport costs should come later.

## Save Requirements

The current save integration persists:

- player Wallet;
- inventory-enabled NPC Wallets and inventories;
- the bounded market ledger;
- stable player/NPC participant IDs.

Before scheduled NPC market simulation is enabled, add:

- current local prices or the state required to reproduce them;
- production/consumption timers;
- any settlement or organization participant state.

## Implementation Phases

### Current implementation

The transaction foundation is complete:

- `ITradeParticipant` is implemented by `PlayerController2D` and `NpcController`.
- `TradeService.TryExecute` performs preflight validation and an all-or-nothing commit.
- `InventoryModel.CanAddItem` prevents partial item placement.
- Wallet-to-Wallet transfers correlate `TradeDebit` and `TradeCredit` entries.
- The bounded `MarketTransaction` ledger is saved.
- Inventory-enabled NPC Wallets are saved.
- A successful commit raises one `TradeCompleted` quest event.

See [TRADE_SYSTEM.md](TRADE_SYSTEM.md) for the runtime API and setup.

### Phase 1: Trading foundation

- [x] Add `InventoryModel.CanAddItem`
- [x] Add `ITradeParticipant`
- [ ] Add configurable trader policies/offers
- [x] Add `TradeRequest` and `TradeResult`
- [x] Add atomic `TradeService`
- [x] Add linked buyer/seller wallet records
- [x] Add `MarketTransaction` and bounded ledger

### Phase 2: Persistence

- [x] Save and restore inventory-enabled NPC wallets
- [x] Save and restore the market ledger
- [ ] Save local-price and simulation state
- [ ] Add migration rules when future market simulation fields change

### Phase 3: Player vendor UI

- Display offers and stock
- Buy through `TradeService`
- Sell through `TradeService`
- Show insufficient-funds and inventory-full feedback
- Display price breakdowns

### Phase 4: NPC market simulation

- Add producer and consumer profiles
- Add scheduled market ticks
- Add offer matching
- Add bounded per-tick transaction volume
- Verify conservation of money and items

### Phase 5: Dynamic pricing and tuning

- Add supply/demand price modifiers
- Add market or settlement modifiers
- Add mana faucets and sinks deliberately
- Add economy diagnostics and graphs
- Stress-test long simulations for inflation, deflation, shortages, and runaway accumulation

## Invariants to Protect

These should eventually become automated tests or runtime assertions:

- Mana balances never become negative.
- Item quantities never become negative.
- A normal trade creates or destroys neither mana nor items.
- Buyer debit equals seller credit.
- Buyer item gain equals seller item loss.
- Failed trades change no state.
- Every completed market transaction has two correlated wallet entries.
- Every participant and item reference resolves after loading.
