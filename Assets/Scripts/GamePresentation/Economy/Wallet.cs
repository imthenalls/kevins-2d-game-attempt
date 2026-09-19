using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Unity facade over the engine-free <see cref="ManaAccount"/>. Owns the inspector fields and
/// mirrors the account's events, so existing scenes, prefabs, and call sites are unchanged while
/// all money logic lives in Game.Data and is unit tested without a scene.
///
/// The same balance is used for trade and spellcasting. Balances never go below zero or above
/// capacity, and every runtime change is recorded with its source.
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
///
/// Runtime API:
///   wallet.CanAfford(amount); wallet.Add(...); wallet.TrySpend(...); wallet.TryTransferTo(...);
///   wallet.TryConsumeMana(...); wallet.RestoreMana(...); wallet.TrySubtract(...);
///   wallet.SetCapacity(...); wallet.GetSaveData(); wallet.LoadSaveData(data);
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

    private ManaAccount account;
    private CharacterStatistics characterStatistics;
    private bool accountSubscribed;

    /// <summary>Current spendable/castable mana. Always between zero and Capacity.</summary>
    public int Balance => Account().Balance;

    /// <summary>Maximum mana this account can currently hold.</summary>
    public int Capacity => Account().Capacity;

    /// <summary>Unused mana storage.</summary>
    public int RemainingCapacity => Account().RemainingCapacity;

    /// <summary>Chronological transaction history, oldest to newest.</summary>
    public List<WalletTransaction> TransactionHistory => Account().TransactionHistory;

    /// <summary>Fired after the balance changes. Argument is the new balance.</summary>
    public event Action<int> OnBalanceChanged;

    /// <summary>Fired after capacity changes. Argument is the new capacity.</summary>
    public event Action<int> OnCapacityChanged;

    /// <summary>Fired after a transaction is added to history.</summary>
    public event Action<WalletTransaction> OnTransactionRecorded;

    private void Awake()
    {
        characterStatistics = GetComponent<CharacterStatistics>();
        Account();
    }

    private void OnValidate()
    {
        manaCapacity = Mathf.Max(0, manaCapacity);
        startingMana = Mathf.Clamp(startingMana, 0, manaCapacity);
        maxTransactionHistory = Mathf.Max(0, maxTransactionHistory);
    }

    /// <summary>True when amount is non-negative and the wallet has enough mana.</summary>
    public bool CanAfford(int amount) => Account().CanAfford(amount);

    /// <summary>True when amount is non-negative and fits within the remaining capacity.</summary>
    public bool CanReceive(int amount) => Account().CanReceive(amount);

    /// <summary>Add mana and record a Credit transaction. Rejects non-positive or overflowing amounts.</summary>
    public bool Add(int amount, string reason = "", string referenceId = "")
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[Wallet] Add requires an amount greater than zero.");
            return false;
        }

        ManaAccount target = Account();
        if (!target.CanReceive(amount))
        {
            Debug.LogWarning("[Wallet] Add rejected because the mana would exceed capacity.");
            return false;
        }

        return target.Add(amount, reason, referenceId);
    }

    /// <summary>Spend mana on a purchase and record a Spend transaction.</summary>
    public bool TrySpend(int amount, string reason = "", string referenceId = "")
    {
        if (!ValidateDebit(amount))
            return false;
        return Account().TrySpend(amount, reason, referenceId);
    }

    /// <summary>Consume mana for spellcasting.</summary>
    public bool TryConsumeMana(int amount, string reason = "Cast spell", string referenceId = "")
    {
        if (!ValidateDebit(amount))
            return false;
        return Account().TryConsumeMana(amount, reason, referenceId);
    }

    /// <summary>Remove mana for a non-purchase reason and record a Subtract transaction.</summary>
    public bool TrySubtract(int amount, string reason = "", string referenceId = "")
    {
        if (!ValidateDebit(amount))
            return false;
        return Account().TrySubtract(amount, reason, referenceId);
    }

    /// <summary>
    /// Atomically transfer mana to another Wallet. Both balances change before events fire and both
    /// ledger entries share the supplied reference id.
    /// </summary>
    public bool TryTransferTo(Wallet recipient, int amount, string reason = "Trade", string referenceId = "")
    {
        if (recipient == null || recipient == this || amount <= 0)
            return false;
        return Account().TryTransferTo(recipient.Account(), amount, reason, referenceId);
    }

    /// <summary>Restore as much mana as fits and return the amount actually restored.</summary>
    public int RestoreMana(int amount, string reason = "Restore mana", string referenceId = "") =>
        Account().RestoreMana(amount, reason, referenceId);

    /// <summary>Change mana capacity, destroying or granting the difference per the flag.</summary>
    public void SetCapacity(int capacity, bool fillAddedCapacity = false)
    {
        Account().SetCapacity(capacity, fillAddedCapacity);
        manaCapacity = Capacity;
    }

    /// <summary>Increase mana capacity and optionally fill the added space.</summary>
    public void IncreaseCapacity(int amount, bool fillAddedCapacity = true)
    {
        Account().IncreaseCapacity(amount, fillAddedCapacity);
        manaCapacity = Capacity;
    }

    /// <summary>Decrease mana capacity and clamp the current balance if necessary.</summary>
    public void DecreaseCapacity(int amount)
    {
        Account().DecreaseCapacity(amount);
        manaCapacity = Capacity;
    }

    /// <summary>Set the balance directly and record the difference as an adjustment.</summary>
    public void SetBalance(int value, string reason = "Set mana", string referenceId = "") =>
        Account().SetBalance(value, reason, referenceId);

    /// <summary>
    /// Initialize balance and capacity without recording a transaction. Used when adding a Wallet
    /// to an existing EntityStats-based player.
    /// </summary>
    public void InitializeMana(int balance, int capacity, bool clearHistory = true)
    {
        ManaAccount target = Account();
        target.InitializeMana(balance, capacity, clearHistory);
        manaCapacity = target.Capacity;
        startingMana = target.Balance;
        OnCapacityChanged?.Invoke(target.Capacity);
        OnBalanceChanged?.Invoke(target.Balance);
    }

    /// <summary>Create a serializable deep copy of the balance and transaction history.</summary>
    public WalletSaveData GetSaveData() => Account().GetSaveData();

    /// <summary>Restore balance and history without creating a new transaction.</summary>
    public void LoadSaveData(WalletSaveData data)
    {
        if (data == null)
            return;

        ManaAccount target = Account();
        target.LoadSaveData(data);
        manaCapacity = target.Capacity;
        startingMana = target.Balance;
        OnCapacityChanged?.Invoke(target.Capacity);
        OnBalanceChanged?.Invoke(target.Balance);
    }

    private ManaAccount Account()
    {
        if (account != null)
            return account;

        account = new ManaAccount(maxTransactionHistory);
        account.InitializeMana(startingMana, Mathf.Max(0, manaCapacity));

        if (!accountSubscribed)
        {
            accountSubscribed = true;
            account.BalanceChanged += balance => OnBalanceChanged?.Invoke(balance);
            account.CapacityChanged += capacity => OnCapacityChanged?.Invoke(capacity);
            account.TransactionRecorded += transaction => OnTransactionRecorded?.Invoke(transaction);
            account.Credited += amount => characterStatistics?.RecordMoneyGained(amount);
        }

        return account;
    }

    private static bool ValidateDebit(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("[Wallet] Debit requires an amount greater than zero.");
            return false;
        }

        return true;
    }
}
