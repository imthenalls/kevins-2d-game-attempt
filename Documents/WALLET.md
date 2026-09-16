# Wallet and Currency

## Overview

`Wallet` owns an actor's canonical mana balance and capacity. The same balance is used for purchasing goods and casting spells. It is intentionally usable by both players and NPCs so trade and magic follow the same accounting rules.

`EntityStats.Mp`, `EntityStats.MaxMp`, `SpendMp`, `RestoreMp`, and related APIs remain available for compatibility. When `EntityStats` is bound to a Wallet, those APIs read or mutate `Wallet.Balance` and `Wallet.Capacity`; there is no second player MP pool.

`CharacterStatistics.TotalMoneyGained` remains a lifetime statistic. `Wallet.Balance` is the amount the player can currently spend. Calling `Wallet.Add` automatically reports the credited amount to `CharacterStatistics` when that component is on the same GameObject.

## Files

| File | Purpose |
|---|---|
| `Assets/Scripts/GamePresentation/Economy/Wallet.cs` | Balance operations, events, transaction records, and serializable wallet snapshot |
| `Assets/Scripts/GamePresentation/GameManagement/SaveData.cs` | Stores `WalletSaveData` inside the main save |
| `Assets/Scripts/GamePresentation/GameManagement/SaveManager.cs` | Captures and restores the player's wallet |

## Unity Setup

1. Select the Player root GameObject or an economic NPC.
2. Add the `Wallet` component.
3. Set **Mana Capacity** and **Starting Mana** for a new game.
4. Set **Max Transaction History**:
   - Default: `200`
   - `0`: do not retain transaction history
   - Oldest entries are discarded when the limit is exceeded.
5. Optionally place `CharacterStatistics` on the same Player GameObject to track lifetime money gained.

`PlayerController2D` finds the Wallet during `Awake`. If the existing Player has no Wallet, it adds one and initializes it from `EntityStats.Starting MP` and `Max MP`. This keeps existing scenes working without manual scene edits.

No reference needs to be assigned to `SaveManager`. It finds the player's Wallet while saving and after loading the saved scene.

SaveManager persists the player's Wallet and Wallets owned by inventory-enabled NPC trade participants. See [TRADE_SYSTEM.md](TRADE_SYSTEM.md) and [MARKET_ECONOMY.md](MARKET_ECONOMY.md).

## Balance API

### Check the balance

```csharp
int currentBalance = wallet.Balance;
int capacity = wallet.Capacity;
int freeSpace = wallet.RemainingCapacity;

if (wallet.CanAfford(50))
{
    // The wallet can safely spend 50.
}
```

`CanAfford` returns `false` for negative amounts.

`CanReceive(amount)` checks whether incoming mana fits within the remaining capacity.

### Add money

```csharp
bool added = wallet.Add(
    amount: 100,
    reason: "Quest reward",
    referenceId: "quest.bandit_king");
```

`Add` returns `false` for zero/negative amounts or when the complete credit would exceed capacity. A successful call:

- Increases `Balance`
- Records a `Credit` transaction
- Fires `OnTransactionRecorded`
- Reports the amount to `CharacterStatistics.RecordMoneyGained`
- Fires `OnBalanceChanged`

### Spend money

```csharp
bool purchased = wallet.TrySpend(
    amount: 25,
    reason: "Bought item",
    referenceId: "item.health_potion");

if (!purchased)
{
    // Invalid amount or insufficient funds. The wallet was not changed.
}
```

Use `TrySpend` for purchases. It records a `Spend` transaction.

Normal item trading should use `TradeService`, which calls `TryTransferTo` and creates correlated `TradeDebit` / `TradeCredit` entries on both participants. Do not debit and credit the two Wallets separately.

### Consume mana for a spell

```csharp
bool cast = wallet.TryConsumeMana(
    amount: 20,
    reason: "Cast fireball",
    referenceId: "spell.fireball");
```

The spell effect must only run if this returns `true`. It records a `Spell` transaction.

Existing code may continue calling `stats.SpendMp(cost)`; a Wallet-bound `EntityStats` delegates to this mana account.

### Restore mana

```csharp
int restored = wallet.RestoreMana(
    amount: 10,
    reason: "Consumed mana tonic",
    referenceId: "item.mana_tonic");
```

Restoration is capped by available capacity and returns the amount actually restored. It records a `Restore` transaction.

### Change capacity

```csharp
wallet.IncreaseCapacity(10, fillAddedCapacity: true);
wallet.DecreaseCapacity(5);
wallet.SetCapacity(100);
```

Reducing capacity destroys and records mana above the new cap. Equipment MP bonuses continue to work through `EntityStats`, which delegates capacity changes to the Wallet.

### Subtract money

```csharp
bool deducted = wallet.TrySubtract(
    amount: 10,
    reason: "Lost wager",
    referenceId: "event.tavern_dice");
```

Use `TrySubtract` for non-purchase deductions such as fines, theft, wagers, or scripted losses. It records a `Subtract` transaction and will not allow the balance to become negative.

## Events

```csharp
wallet.OnBalanceChanged += newBalance =>
{
    Debug.Log($"Balance: {newBalance}");
};

wallet.OnTransactionRecorded += transaction =>
{
    Debug.Log($"{transaction.type}: {transaction.amount}");
};
```

| Event | Argument | Fires |
|---|---|---|
| `OnBalanceChanged` | New balance | After any successful mana credit/debit, adjustment, initialization, or load |
| `OnCapacityChanged` | New capacity | After a capacity change, initialization, or load |
| `OnTransactionRecorded` | New `WalletTransaction` | After any recorded runtime mana change |

## Transaction History

`Wallet.TransactionHistory` exposes the retained entries in chronological order, oldest to newest.

Each `WalletTransaction` stores:

| Field | Description |
|---|---|
| `transactionId` | Generated unique ID |
| `utcTimestamp` | ISO-8601 UTC timestamp |
| `type` | `Credit`, `Spend`, `Subtract`, `Spell`, `Restore`, `CapacityAdjustment`, `Adjustment`, `Migration`, `TradeDebit`, or `TradeCredit` |
| `amount` | Signed amount: positive for credits, negative for debits |
| `balanceAfter` | Wallet balance after the transaction |
| `reason` | Human-readable/source description supplied by the caller |
| `referenceId` | Stable game-data ID such as a quest, item, NPC, or event ID |

Use stable `referenceId` values for filtering and debugging. Do not use display names as identifiers.

## Save and Load

`SaveManager.Save()` calls `wallet.GetSaveData()` and writes:

- Current balance
- Current capacity
- Retained transaction history

`SaveManager.RestoreSceneState()` finds the Wallet on the loaded Player and calls `wallet.LoadSaveData(data.wallet)`.

Loading:

- Does not create a transaction
- Does not increase `TotalMoneyGained`
- Restores only the newest entries when the saved ledger exceeds the configured history limit
- Clamps an invalid negative saved balance to zero
- Clamps balance to the saved capacity

Pre-unification saves are migrated once. Their former wallet balance and player MP are added together so neither owned resource is silently discarded. Capacity expands to hold the combined amount, and the MP portion is recorded as a `Migration` transaction.

## Remaining Economy Work

The wallet is the storage and accounting foundation. The market must treat players and NPCs as the same kind of trade participant; see [MARKET_ECONOMY.md](MARKET_ECONOMY.md). These systems still need to be built:

- Currency HUD bound to `OnBalanceChanged`
- Transaction-history/debug viewer
- Quest action for currency rewards
- Currency loot/reward integration
- Vendor/shop UI
- Buy prices, pricing rules, and optional vendor modifiers
- Clear player feedback for insufficient funds
