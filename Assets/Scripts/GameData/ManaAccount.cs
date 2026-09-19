using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free mana account: canonical balance, capacity, and an optional bounded transaction
    /// history. Balances never go below zero or above capacity, and every runtime change is
    /// recorded with its source.
    ///
    /// This is the authoritative money state. The Unity <c>Wallet</c> MonoBehaviour is a thin
    /// facade that owns the inspector fields and forwards to an instance of this class, so money
    /// logic can be tested without a scene.
    ///
    /// Runtime API: CanAfford, CanReceive, Add, TrySpend, TrySubtract, TryConsumeMana, RestoreMana,
    /// TryTransferTo, SetCapacity, IncreaseCapacity, DecreaseCapacity, SetBalance, InitializeMana,
    /// GetSaveData, LoadSaveData.
    /// </summary>
    public sealed class ManaAccount
    {
        private readonly List<WalletTransaction> transactionHistory = new List<WalletTransaction>();
        private readonly int maxTransactionHistory;

        /// <summary>Current spendable/castable mana. Always between zero and Capacity.</summary>
        public int Balance { get; private set; }

        /// <summary>Maximum mana this account can currently hold.</summary>
        public int Capacity { get; private set; }

        /// <summary>Unused mana storage.</summary>
        public int RemainingCapacity => Capacity - Balance;

        /// <summary>Chronological transaction history, oldest to newest.</summary>
        public List<WalletTransaction> TransactionHistory => transactionHistory;

        /// <summary>Fired after the balance changes. Argument is the new balance.</summary>
        public event Action<int> BalanceChanged;

        /// <summary>Fired after capacity changes. Argument is the new capacity.</summary>
        public event Action<int> CapacityChanged;

        /// <summary>Fired after a transaction is added to history.</summary>
        public event Action<WalletTransaction> TransactionRecorded;

        /// <summary>Fired whenever mana is credited (added directly or received in a transfer).</summary>
        public event Action<int> Credited;

        public ManaAccount(int maxTransactionHistory = 200)
        {
            this.maxTransactionHistory = Math.Max(0, maxTransactionHistory);
        }

        /// <summary>True when amount is non-negative and the account has enough mana.</summary>
        public bool CanAfford(int amount) => amount >= 0 && Balance >= amount;

        /// <summary>True when amount is non-negative and fits within the remaining capacity.</summary>
        public bool CanReceive(int amount) => amount >= 0 && amount <= RemainingCapacity;

        /// <summary>
        /// Add mana and record a Credit transaction. Returns false for non-positive amounts or when
        /// the addition would exceed capacity.
        /// </summary>
        public bool Add(int amount, string reason = "", string referenceId = "")
        {
            if (amount <= 0 || !CanReceive(amount))
                return false;

            Balance += amount;
            RecordTransaction(WalletTransactionType.Credit, amount, reason, referenceId);
            Credited?.Invoke(amount);
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        /// <summary>Spend mana on a purchase and record a Spend transaction.</summary>
        public bool TrySpend(int amount, string reason = "", string referenceId = "") =>
            TryDebit(amount, WalletTransactionType.Spend, reason, referenceId);

        /// <summary>Remove mana for a non-purchase reason and record a Subtract transaction.</summary>
        public bool TrySubtract(int amount, string reason = "", string referenceId = "") =>
            TryDebit(amount, WalletTransactionType.Subtract, reason, referenceId);

        /// <summary>Consume mana for spellcasting.</summary>
        public bool TryConsumeMana(int amount, string reason = "Cast spell", string referenceId = "") =>
            TryDebit(amount, WalletTransactionType.Spell, reason, referenceId);

        /// <summary>
        /// Restore as much mana as fits and return the amount actually restored. A full account
        /// returns zero without recording a transaction.
        /// </summary>
        public int RestoreMana(int amount, string reason = "Restore mana", string referenceId = "")
        {
            if (amount <= 0)
                return 0;

            int restored = Math.Min(amount, RemainingCapacity);
            if (restored <= 0)
                return 0;

            Balance += restored;
            RecordTransaction(WalletTransactionType.Restore, restored, reason, referenceId);
            BalanceChanged?.Invoke(Balance);
            return restored;
        }

        /// <summary>
        /// Atomically transfer mana to another account. Both balances change before events fire and
        /// both ledger entries share the supplied reference id.
        /// </summary>
        public bool TryTransferTo(
            ManaAccount recipient,
            int amount,
            string reason = "Trade",
            string referenceId = "")
        {
            if (recipient == null || ReferenceEquals(recipient, this) || amount <= 0)
                return false;
            if (!CanAfford(amount) || !recipient.CanReceive(amount))
                return false;

            Balance -= amount;
            recipient.Balance += amount;

            RecordTransaction(WalletTransactionType.TradeDebit, -amount, reason, referenceId);
            recipient.RecordTransaction(WalletTransactionType.TradeCredit, amount, reason, referenceId);

            recipient.Credited?.Invoke(amount);
            BalanceChanged?.Invoke(Balance);
            recipient.BalanceChanged?.Invoke(recipient.Balance);
            return true;
        }

        /// <summary>
        /// Change mana capacity. Reducing capacity destroys any excess mana and records that loss.
        /// When fillAddedCapacity is true, newly added capacity is restored as mana.
        /// </summary>
        public void SetCapacity(int capacity, bool fillAddedCapacity = false)
        {
            capacity = Math.Max(0, capacity);
            if (capacity == Capacity)
                return;

            int previousCapacity = Capacity;
            Capacity = capacity;
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

            CapacityChanged?.Invoke(Capacity);
            if (balanceChanged)
                BalanceChanged?.Invoke(Balance);
        }

        /// <summary>Increase mana capacity and optionally fill the added space.</summary>
        public void IncreaseCapacity(int amount, bool fillAddedCapacity = true)
        {
            if (amount <= 0 || Capacity > int.MaxValue - amount)
                return;
            SetCapacity(Capacity + amount, fillAddedCapacity);
        }

        /// <summary>Decrease mana capacity and clamp the current balance if necessary.</summary>
        public void DecreaseCapacity(int amount)
        {
            if (amount <= 0)
                return;
            SetCapacity(Math.Max(0, Capacity - amount));
        }

        /// <summary>
        /// Set the balance directly and record the difference as an adjustment. Intended for
        /// compatibility APIs and administrative effects, not save loading.
        /// </summary>
        public void SetBalance(int value, string reason = "Set mana", string referenceId = "")
        {
            int next = Clamp(value, 0, Capacity);
            int difference = next - Balance;
            if (difference == 0)
                return;

            Balance = next;
            RecordTransaction(WalletTransactionType.Adjustment, difference, reason, referenceId);
            BalanceChanged?.Invoke(Balance);
        }

        /// <summary>
        /// Initialize balance and capacity without recording a transaction (used for new games and
        /// when a Wallet is added to an existing entity).
        /// </summary>
        public void InitializeMana(int balance, int capacity, bool clearHistory = true)
        {
            Capacity = Math.Max(0, capacity);
            Balance = Clamp(balance, 0, Capacity);
            if (clearHistory)
                transactionHistory.Clear();
            CapacityChanged?.Invoke(Capacity);
            BalanceChanged?.Invoke(Balance);
        }

        /// <summary>Create a serializable deep copy of the balance and transaction history.</summary>
        public WalletSaveData GetSaveData()
        {
            var data = new WalletSaveData
            {
                balance = Balance,
                capacity = Capacity,
            };
            foreach (WalletTransaction transaction in transactionHistory)
                data.transactions.Add(transaction.Clone());
            return data;
        }

        /// <summary>
        /// Restore balance and history without creating a transaction. Negative balances clamp to zero.
        /// </summary>
        public void LoadSaveData(WalletSaveData data)
        {
            if (data == null)
                return;

            Capacity = Math.Max(0, data.capacity);
            Balance = Clamp(data.balance, 0, Capacity);
            transactionHistory.Clear();

            if (maxTransactionHistory > 0 && data.transactions != null)
            {
                int start = Math.Max(0, data.transactions.Count - maxTransactionHistory);
                for (int i = start; i < data.transactions.Count; i++)
                {
                    if (data.transactions[i] != null)
                        transactionHistory.Add(data.transactions[i].Clone());
                }
            }

            CapacityChanged?.Invoke(Capacity);
            BalanceChanged?.Invoke(Balance);
        }

        private bool TryDebit(int amount, WalletTransactionType transactionType, string reason, string referenceId)
        {
            if (amount <= 0 || !CanAfford(amount))
                return false;

            Balance -= amount;
            RecordTransaction(transactionType, -amount, reason, referenceId);
            BalanceChanged?.Invoke(Balance);
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

            TransactionRecorded?.Invoke(transaction);
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
