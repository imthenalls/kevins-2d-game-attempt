using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Owns an actor's canonical mana balance, storage capacity, and optional bounded transaction
/// history. The same balance is used for trade and spellcasting. Balances never go below zero
/// or above capacity, and every runtime change is recorded with its source.
///
/// Unity setup:
///   1. Add Wallet to the Player root GameObject or any economic NPC.
///   2. Set Mana Capacity and Starting Mana for a new game.
///   3. PlayerController2D adds this component automatically when it is absent and initializes
///      it from EntityStats' legacy Starting MP / Max MP fields.
///   4. Set Max Transaction History to the number of entries retained in saves
///      (default 200; set to 0 to disable history).
///   5. Optionally add CharacterStatistics to the same GameObject. Wallet automatically
///      reports credited money to CharacterStatistics.TotalMoneyGained.
///   6. SaveManager currently persists the Player wallet. NPC wallet persistence must be
///      added before NPC market simulation is enabled.
///
/// Runtime API:
///   wallet.CanAfford(amount);
///   wallet.Add(amount, reason, referenceId);
///   wallet.TrySpend(amount, reason, referenceId);
///   wallet.TryTransferTo(recipient, amount, reason, referenceId);
///   wallet.TryConsumeMana(amount, reason, referenceId);
///   wallet.RestoreMana(amount, reason, referenceId);
///   wallet.TrySubtract(amount, reason, referenceId);
///   wallet.SetCapacity(capacity, fillAddedCapacity);
///   wallet.GetSaveData();
///   wallet.LoadSaveData(data);
/// </summary>
[DisallowMultipleComponent]
public class Wallet : MonoBehaviour
{
    [Header("Mana")]
    [SerializeField, Min(0)] private int manaCapacity = 50;
    [FormerlySerializedAs("startingBalance")]
    [SerializeField, Min(0)] private int startingMana = 50;

    [Header("Transaction History")]
    [Tooltip("Maximum transaction entries retained and saved. Set to 0 to disable history.")]
    [SerializeField, Min(0)] private int maxTransactionHistory = 200;

    private readonly List<WalletTransaction> transactionHistory = new();
    private CharacterStatistics characterStatistics;

    /// <summary>Current spendable/castable mana. Always between zero and Capacity.</summary>
    public int Balance { get; private set; }

    /// <summary>Maximum mana this account can currently hold.</summary>
    public int Capacity { get; private set; }

    /// <summary>Unused mana storage.</summary>
    public int RemainingCapacity => Capacity - Balance;

    /// <summary>Read-only chronological transaction history, oldest to newest.</summary>
    public IReadOnlyList<WalletTransaction> TransactionHistory => transactionHistory;

    /// <summary>Fired after the balance changes. Argument is the new balance.</summary>
    public event Action<int> OnBalanceChanged;

    /// <summary>Fired after capacity changes. Argument is the new capacity.</summary>
    public event Action<int> OnCapacityChanged;

    /// <summary>Fired after a transaction is added to history.</summary>
    public event Action<WalletTransaction> OnTransactionRecorded;

    private void Awake()
    {
        Capacity = Mathf.Max(0, manaCapacity);
        Balance = Mathf.Clamp(startingMana, 0, Capacity);
        characterStatistics = GetComponent<CharacterStatistics>();
    }

    private void OnValidate()
    {
        manaCapacity = Mathf.Max(0, manaCapacity);
        startingMana = Mathf.Clamp(startingMana, 0, manaCapacity);
        maxTransactionHistory = Mathf.Max(0, maxTransactionHistory);
    }

    /// <summary>True when amount is non-negative and the wallet has enough currency.</summary>
    public bool CanAfford(int amount) => amount >= 0 && Balance >= amount;

    /// <summary>True when amount is non-negative and fits within the remaining capacity.</summary>
    public bool CanReceive(int amount) => amount >= 0 && amount <= RemainingCapacity;

    /// <summary>
    /// Add currency and record a Credit transaction.
    /// Returns false for non-positive amounts or if the addition would overflow Int32.
    /// </summary>
    public bool Add(int amount, string reason = "", string referenceId = "")
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[Wallet] Add requires an amount greater than zero.");
            return false;
        }

        if (!CanReceive(amount))
        {
            Debug.LogWarning("[Wallet] Add rejected because the mana would exceed capacity.");
            return false;
        }

        Balance += amount;
        RecordTransaction(WalletTransactionType.Credit, amount, reason, referenceId);
        characterStatistics?.RecordMoneyGained(amount);
        OnBalanceChanged?.Invoke(Balance);
        return true;
    }

    /// <summary>
    /// Spend currency on a purchase and record a Spend transaction.
    /// Returns false without changing the wallet when amount is invalid or unaffordable.
    /// </summary>
    public bool TrySpend(int amount, string reason = "", string referenceId = "")
    {
        return TryDebit(amount, WalletTransactionType.Spend, reason, referenceId);
    }

    /// <summary>
    /// Atomically transfer mana to another Wallet. Both balances are changed before events fire,
    /// and both ledger entries share the supplied reference ID.
    /// </summary>
    public bool TryTransferTo(
        Wallet recipient,
        int amount,
        string reason = "Trade",
        string referenceId = "")
    {
        if (recipient == null || recipient == this || amount <= 0)
            return false;
        if (!CanAfford(amount) || !recipient.CanReceive(amount))
            return false;

        Balance -= amount;
        recipient.Balance += amount;

        RecordTransaction(
            WalletTransactionType.TradeDebit,
            -amount,
            reason,
            referenceId);
        recipient.RecordTransaction(
            WalletTransactionType.TradeCredit,
            amount,
            reason,
            referenceId);

        recipient.characterStatistics?.RecordMoneyGained(amount);
        OnBalanceChanged?.Invoke(Balance);
        recipient.OnBalanceChanged?.Invoke(recipient.Balance);
        return true;
    }

    /// <summary>
    /// Consume mana for spellcasting. Returns false without applying a debit when unaffordable.
    /// </summary>
    public bool TryConsumeMana(int amount, string reason = "Cast spell", string referenceId = "")
    {
        return TryDebit(amount, WalletTransactionType.Spell, reason, referenceId);
    }

    /// <summary>
    /// Restore as much mana as fits and return the amount actually restored.
    /// A full account returns zero without recording a transaction.
    /// </summary>
    public int RestoreMana(int amount, string reason = "Restore mana", string referenceId = "")
    {
        if (amount <= 0) return 0;

        int restored = Mathf.Min(amount, RemainingCapacity);
        if (restored <= 0) return 0;

        Balance += restored;
        RecordTransaction(WalletTransactionType.Restore, restored, reason, referenceId);
        OnBalanceChanged?.Invoke(Balance);
        return restored;
    }

    /// <summary>
    /// Remove currency for a non-purchase reason and record a Subtract transaction.
    /// Returns false without changing the wallet when amount is invalid or unaffordable.
    /// </summary>
    public bool TrySubtract(int amount, string reason = "", string referenceId = "")
    {
        return TryDebit(amount, WalletTransactionType.Subtract, reason, referenceId);
    }

    /// <summary>
    /// Change mana capacity. Reducing capacity destroys any excess mana and records that loss.
    /// When fillAddedCapacity is true, newly added capacity is restored as mana.
    /// </summary>
    public void SetCapacity(int capacity, bool fillAddedCapacity = false)
    {
        capacity = Mathf.Max(0, capacity);
        if (capacity == Capacity) return;

        int previousCapacity = Capacity;
        Capacity = capacity;
        manaCapacity = capacity;
        bool balanceChanged = false;

        if (Balance > Capacity)
        {
            int lost = Balance - Capacity;
            Balance = Capacity;
            balanceChanged = true;
            RecordTransaction(
                WalletTransactionType.CapacityAdjustment,
                -lost,
                "Mana lost when capacity decreased",
                "mana.capacity");
        }
        else if (fillAddedCapacity && Capacity > previousCapacity)
        {
            int restored = Capacity - previousCapacity;
            Balance += restored;
            balanceChanged = true;
            RecordTransaction(
                WalletTransactionType.Restore,
                restored,
                "Mana restored by capacity increase",
                "mana.capacity");
        }

        OnCapacityChanged?.Invoke(Capacity);
        if (balanceChanged)
            OnBalanceChanged?.Invoke(Balance);
    }

    /// <summary>Increase mana capacity and optionally fill the added space.</summary>
    public void IncreaseCapacity(int amount, bool fillAddedCapacity = true)
    {
        if (amount <= 0 || Capacity > int.MaxValue - amount) return;
        SetCapacity(Capacity + amount, fillAddedCapacity);
    }

    /// <summary>Decrease mana capacity and clamp the current balance if necessary.</summary>
    public void DecreaseCapacity(int amount)
    {
        if (amount <= 0) return;
        SetCapacity(Mathf.Max(0, Capacity - amount));
    }

    /// <summary>
    /// Set the balance directly and record the difference as an adjustment.
    /// Intended for compatibility APIs and administrative gameplay effects, not save loading.
    /// </summary>
    public void SetBalance(int value, string reason = "Set mana", string referenceId = "")
    {
        int next = Mathf.Clamp(value, 0, Capacity);
        int difference = next - Balance;
        if (difference == 0) return;

        Balance = next;
        RecordTransaction(WalletTransactionType.Adjustment, difference, reason, referenceId);
        OnBalanceChanged?.Invoke(Balance);
    }

    /// <summary>
    /// Initialize balance and capacity without recording a transaction.
    /// Used when adding a Wallet to an existing EntityStats-based player.
    /// </summary>
    public void InitializeMana(int balance, int capacity, bool clearHistory = true)
    {
        Capacity = Mathf.Max(0, capacity);
        Balance = Mathf.Clamp(balance, 0, Capacity);
        manaCapacity = Capacity;
        startingMana = Balance;
        if (clearHistory)
            transactionHistory.Clear();
        OnCapacityChanged?.Invoke(Capacity);
        OnBalanceChanged?.Invoke(Balance);
    }

    /// <summary>Create a serializable deep copy of the balance and transaction history.</summary>
    public WalletSaveData GetSaveData()
    {
        var data = new WalletSaveData
        {
            balance = Balance,
            capacity = Capacity,
        };
        foreach (var transaction in transactionHistory)
            data.transactions.Add(transaction.Clone());
        return data;
    }

    /// <summary>
    /// Restore balance and history without creating a new transaction.
    /// Invalid negative balances are clamped to zero.
    /// </summary>
    public void LoadSaveData(WalletSaveData data)
    {
        if (data == null) return;

        Capacity = Mathf.Max(0, data.capacity);
        Balance = Mathf.Clamp(data.balance, 0, Capacity);
        manaCapacity = Capacity;
        startingMana = Balance;
        transactionHistory.Clear();

        if (maxTransactionHistory > 0 && data.transactions != null)
        {
            int start = Mathf.Max(0, data.transactions.Count - maxTransactionHistory);
            for (int i = start; i < data.transactions.Count; i++)
            {
                if (data.transactions[i] != null)
                    transactionHistory.Add(data.transactions[i].Clone());
            }
        }

        OnCapacityChanged?.Invoke(Capacity);
        OnBalanceChanged?.Invoke(Balance);
    }

    private bool TryDebit(
        int amount,
        WalletTransactionType transactionType,
        string reason,
        string referenceId)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[Wallet] Debit requires an amount greater than zero.");
            return false;
        }

        if (!CanAfford(amount))
            return false;

        Balance -= amount;
        RecordTransaction(transactionType, -amount, reason, referenceId);
        OnBalanceChanged?.Invoke(Balance);
        return true;
    }

    private void RecordTransaction(
        WalletTransactionType transactionType,
        int signedAmount,
        string reason,
        string referenceId)
    {
        var transaction = new WalletTransaction
        {
            transactionId = Guid.NewGuid().ToString("N"),
            utcTimestamp = DateTime.UtcNow.ToString("O"),
            type = transactionType,
            amount = signedAmount,
            balanceAfter = Balance,
            reason = reason ?? string.Empty,
            referenceId = referenceId ?? string.Empty,
        };

        if (maxTransactionHistory > 0)
        {
            transactionHistory.Add(transaction);
            while (transactionHistory.Count > maxTransactionHistory)
                transactionHistory.RemoveAt(0);
        }

        OnTransactionRecorded?.Invoke(transaction);
    }
}

/// <summary>
/// Classification for a wallet transaction.
/// Unity setup: none. Wallet assigns this when recording balance changes.
/// </summary>
public enum WalletTransactionType
{
    Credit,
    Spend,
    Subtract,
    Spell,
    Restore,
    CapacityAdjustment,
    Adjustment,
    Migration,
    TradeDebit,
    TradeCredit,
}

/// <summary>
/// Serializable immutable-by-convention record of one wallet balance change.
/// Unity setup: none. Created by Wallet and copied into WalletSaveData.
/// </summary>
[Serializable]
public class WalletTransaction
{
    public string transactionId;
    public string utcTimestamp;
    public WalletTransactionType type;
    public int amount;
    public int balanceAfter;
    public string reason;
    public string referenceId;

    public WalletTransaction Clone()
    {
        return new WalletTransaction
        {
            transactionId = transactionId,
            utcTimestamp = utcTimestamp,
            type = type,
            amount = amount,
            balanceAfter = balanceAfter,
            reason = reason,
            referenceId = referenceId,
        };
    }
}

/// <summary>
/// Serializable wallet snapshot containing the current balance and transaction ledger.
/// Unity setup: none. SaveManager obtains and restores it through Wallet.
/// </summary>
[Serializable]
public class WalletSaveData
{
    public int balance;
    public int capacity;
    public List<WalletTransaction> transactions = new();
}
