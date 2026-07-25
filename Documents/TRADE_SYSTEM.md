# Atomic Trade System

## Overview

`TradeService` is the single transaction path for player-to-NPC and NPC-to-NPC item trades. A trade exchanges one item type for mana and either commits completely or changes nothing.

Successful trades:

1. Move the complete item quantity from seller to buyer.
2. Move the complete mana price from buyer to seller.
3. Correlate both Wallet entries with one trade ID.
4. Add one `MarketTransaction` to the saved market ledger.
5. Raise one `TradeCompleted` quest event.
6. Notify inventory and trade observers after the committed state is visible.

No shop UI is included yet. UI and market AI should both construct `TradeRequest` objects and use this service.

## Files

| File | Purpose |
|---|---|
| `Assets/Scripts/Economy/ITradeParticipant.cs` | Shared participant identity, Wallet, and Inventory contract |
| `Assets/Scripts/Economy/TradeService.cs` | Validation, commit, result types, and market ledger |
| `Assets/Scripts/Economy/Wallet.cs` | Atomic Wallet-to-Wallet mana transfer |
| `Assets/Scripts/Inventory/InventoryModel.cs` | Capacity preflight and transaction-safe item transfer |
| `Assets/Scripts/Player/PlayerController2D.cs` | Built-in player participant |
| `Assets/Scripts/NPCs/NpcController.cs` | Built-in NPC participant |

## Unity Setup

### Player

No additional component is required:

- `PlayerController2D` implements `ITradeParticipant`.
- Participant ID is `player`.
- `PlayerController2D` finds/adds its Wallet.
- Player inventory comes from `InventoryUI.Model`.

The InventoryUI must have completed `Awake` before a player trade is attempted.

### Trading NPC

1. Add `NpcController` to the NPC.
2. Give it a unique, stable **Npc Id**.
3. Enable **Has Inventory**.
4. Set inventory rows and columns.
5. Configure **Trader Starting Mana** and **Trader Mana Capacity** on `NpcController`.
6. Optionally add Wallet manually and configure it directly.

If Wallet is omitted, an inventory-enabled NPC adds one during `Awake` and initializes it from the NPC trader fields (defaults: 50 mana, 500 capacity). A manually attached Wallet takes precedence.

The NPC's participant ID is its `Npc Id`.

## Executing a Trade

### Player buys from an NPC

```csharp
var request = new TradeRequest
{
    buyer = playerController,
    seller = vendorController,
    item = healthPotion,
    quantity = 2,
    unitPrice = 8,
    marketId = "willowmere",
    reason = "Bought health potions",
};

TradeResult result = TradeService.TryExecute(request);
if (!result.Success)
    Debug.LogWarning(result.Message);
```

### Player sells to an NPC

Use the same API with participant roles reversed:

```csharp
var request = new TradeRequest
{
    buyer = vendorController,
    seller = playerController,
    item = commonHerb,
    quantity = 5,
    unitPrice = 3,
    marketId = "willowmere",
    reason = "Sold common herbs",
};

TradeResult result = TradeService.TryExecute(request);
```

NPC-to-NPC trading uses exactly the same request.

## Validation

Before mutation, `TradeService` verifies:

- request, buyer, seller, and item exist;
- participant IDs are stable and different;
- participants do not share a Wallet or inventory;
- quantity and unit price are positive;
- total price does not overflow an integer;
- both participants have Wallets and inventories;
- seller has the complete item quantity;
- buyer can fit the complete item quantity;
- buyer can afford the complete price;
- seller can receive the complete price without exceeding mana capacity.

`TradeFailure` classifies the failure. `TradeResult.Message` provides a user/debug explanation.

## Atomic Commit

Inventory transfer is staged without firing `OnChanged`. If the Wallet transfer unexpectedly fails, both inventories restore their exact slot snapshots before observers are notified.

Wallet transfer changes both balances before firing either balance event. The buyer receives a `TradeDebit` entry and the seller receives a `TradeCredit` entry, both referencing the same trade ID.

A failed trade creates:

- no item movement;
- no mana movement;
- no Wallet transaction;
- no market transaction;
- no quest event.

## Market Ledger

`TradeService.CompletedTrades` retains the newest 500 `MarketTransaction` records.

Each record contains:

- trade ID and UTC timestamp;
- buyer and seller participant IDs;
- item ID and quantity;
- unit and total price;
- market ID;
- reason.

`SaveManager` stores this ledger in `SaveData.marketTransactions`. It also stores Wallet snapshots for inventory-enabled NPCs, so their post-trade mana persists with their inventory.

## Quest Event

After the trade is committed and recorded:

```csharp
QuestEventBus.Raise("TradeCompleted", item.itemId, quantity);
```

Quest JSON can count units traded:

```json
{
  "id": "sell_common_herbs",
  "eventType": "TradeCompleted",
  "targetId": "common_herb",
  "requiredCount": 5
}
```

The event currently represents all completed trades, including NPC-to-NPC exchanges. A future quest that must distinguish buying, selling, a participant, or a market should use additional explicitly named event types rather than raising `TradeCompleted` multiple times for one transaction.

## Remaining UI and Market Work

- Vendor/shop UI that builds requests and displays `TradeFailure`
- NPC buy/sell policies and minimum mana reserves
- Offer matching and scheduled market ticks
- Dynamic pricing
- Trade filtering by item, participant, or market
- Debug market-ledger viewer
