using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Classification for a mana-account transaction.
    /// Unity setup: none. ManaAccount assigns this when recording balance changes.
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
    /// Serializable immutable-by-convention record of one mana balance change.
    /// Unity setup: none. Created by ManaAccount and copied into WalletSaveData.
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
    /// Serializable mana snapshot containing the current balance and transaction ledger.
    /// Unity setup: none. Wallet and SaveManager obtain and restore it through ManaAccount.
    /// </summary>
    [Serializable]
    public class WalletSaveData
    {
        public int balance;
        public int capacity;
        public List<WalletTransaction> transactions = new();
    }
}
