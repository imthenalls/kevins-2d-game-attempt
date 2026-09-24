using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free item-for-mana exchange service shared by player-to-NPC and NPC-to-NPC trading.
    /// It validates both inventories and mana accounts before mutation, commits item and mana
    /// ownership together, and records a correlated market entry. Plain C#, lives in Game.Data and is
    /// unit-tested without a scene.
    ///
    /// Unity setup: none. This is a static Core service. Unity-side reactions (for example raising a
    /// quest event) subscribe to <see cref="OnTradeCompleted"/> in the Shell.
    ///
    /// Runtime API:
    ///   TradeResult result = TradeService.TryExecute(request);
    ///   TradeService.OnTradeCompleted += HandleTrade;
    ///   TradeService.CompletedTrades exposes the retained market ledger.
    /// </summary>
    public static class TradeService
    {
        private const int MaxCompletedTrades = 500;
        private static readonly List<MarketTransaction> completedTrades = new List<MarketTransaction>();

        /// <summary>The retained market ledger, oldest to newest (read-only by convention).</summary>
        public static List<MarketTransaction> CompletedTrades => completedTrades;

        /// <summary>Raised after a successful commit. The Shell forwards this to quests/UI.</summary>
        public static event Action<MarketTransaction> OnTradeCompleted;

        /// <summary>
        /// Validate and commit one complete trade. A failed result leaves both inventories,
        /// both mana accounts, transaction histories, and the market ledger unchanged.
        /// </summary>
        public static TradeResult TryExecute(TradeRequest request)
        {
            TradeResult validation = Validate(request);
            if (!validation.Success)
                return validation;

            long totalLong = (long)request.quantity * request.unitPrice;
            int totalPrice = (int)totalLong;
            string tradeId = Guid.NewGuid().ToString("N");
            string reason = string.IsNullOrWhiteSpace(request.reason)
                ? $"Trade {request.item.ItemId}"
                : request.reason;

            InventoryModel sellerInventory = request.seller.TradeInventory;
            InventoryModel buyerInventory = request.buyer.TradeInventory;

            if (!sellerInventory.TryTransferItemTo(
                    buyerInventory,
                    request.item,
                    request.quantity,
                    out InventoryModel.InventorySnapshot sellerBefore,
                    out InventoryModel.InventorySnapshot buyerBefore))
            {
                return TradeResult.Failed(
                    TradeFailure.CommitFailed,
                    "Inventory state changed before the trade could commit.");
            }

            bool manaTransferred = request.buyer.TradeWallet.TryTransferTo(
                request.seller.TradeWallet,
                totalPrice,
                reason,
                tradeId);

            if (!manaTransferred)
            {
                sellerInventory.RestoreSnapshot(sellerBefore);
                buyerInventory.RestoreSnapshot(buyerBefore);
                return TradeResult.Failed(
                    TradeFailure.CommitFailed,
                    "Mana state changed before the trade could commit.");
            }

            sellerInventory.NotifyChanged();
            buyerInventory.NotifyChanged();

            var transaction = new MarketTransaction
            {
                tradeId = tradeId,
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                buyerParticipantId = request.buyer.TradeParticipantId,
                sellerParticipantId = request.seller.TradeParticipantId,
                itemId = request.item.ItemId,
                quantity = request.quantity,
                unitPrice = request.unitPrice,
                totalPrice = totalPrice,
                marketId = request.marketId ?? string.Empty,
                reason = reason,
            };

            completedTrades.Add(transaction);
            TrimHistory();
            OnTradeCompleted?.Invoke(transaction.Clone());
            return TradeResult.Succeeded(transaction.Clone());
        }

        /// <summary>Create a serializable deep copy of the retained market ledger.</summary>
        public static List<MarketTransaction> GetSaveData()
        {
            var result = new List<MarketTransaction>(completedTrades.Count);
            foreach (MarketTransaction transaction in completedTrades)
                result.Add(transaction.Clone());
            return result;
        }

        /// <summary>Restore the retained market ledger without raising completion events.</summary>
        public static void LoadSaveData(List<MarketTransaction> transactions)
        {
            completedTrades.Clear();
            if (transactions == null) return;

            int start = Math.Max(0, transactions.Count - MaxCompletedTrades);
            for (int i = start; i < transactions.Count; i++)
            {
                if (transactions[i] != null)
                    completedTrades.Add(transactions[i].Clone());
            }
        }

        private static TradeResult Validate(TradeRequest request)
        {
            if (request == null)
                return TradeResult.Failed(TradeFailure.InvalidRequest, "Trade request is null.");
            if (request.buyer == null || request.seller == null)
                return TradeResult.Failed(TradeFailure.MissingParticipant, "Buyer and seller are required.");
            if (ReferenceEquals(request.buyer, request.seller))
                return TradeResult.Failed(TradeFailure.SameParticipant, "Buyer and seller must be different.");
            if (string.IsNullOrWhiteSpace(request.buyer.TradeParticipantId) ||
                string.IsNullOrWhiteSpace(request.seller.TradeParticipantId))
            {
                return TradeResult.Failed(
                    TradeFailure.MissingParticipantId,
                    "Both participants require stable IDs.");
            }
            if (string.Equals(
                    request.buyer.TradeParticipantId,
                    request.seller.TradeParticipantId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return TradeResult.Failed(
                    TradeFailure.SameParticipant,
                    "Buyer and seller IDs must be different.");
            }
            if (request.item == null || string.IsNullOrWhiteSpace(request.item.ItemId))
                return TradeResult.Failed(TradeFailure.InvalidItem, "A stable item id is required.");
            if (request.quantity <= 0)
                return TradeResult.Failed(TradeFailure.InvalidQuantity, "Quantity must be positive.");
            if (request.unitPrice <= 0)
                return TradeResult.Failed(TradeFailure.InvalidPrice, "Unit price must be positive.");

            long totalPrice = (long)request.quantity * request.unitPrice;
            if (totalPrice > int.MaxValue)
                return TradeResult.Failed(TradeFailure.PriceOverflow, "Total price exceeds Int32.");

            if (request.buyer.TradeInventory == null || request.seller.TradeInventory == null)
                return TradeResult.Failed(
                    TradeFailure.MissingInventory,
                    "Both participants require inventories.");
            if (request.buyer.TradeWallet == null || request.seller.TradeWallet == null)
                return TradeResult.Failed(
                    TradeFailure.MissingWallet,
                    "Both participants require mana accounts.");
            if (request.buyer.TradeInventory == request.seller.TradeInventory ||
                request.buyer.TradeWallet == request.seller.TradeWallet)
            {
                return TradeResult.Failed(
                    TradeFailure.SharedOwnership,
                    "Buyer and seller cannot share an inventory or account.");
            }
            if (!request.seller.TradeInventory.HasItem(request.item, request.quantity))
                return TradeResult.Failed(TradeFailure.InsufficientStock, "Seller lacks the requested stock.");
            if (!request.buyer.TradeInventory.CanAddItem(request.item, request.quantity))
                return TradeResult.Failed(TradeFailure.InsufficientInventorySpace, "Buyer inventory is full.");
            if (!request.buyer.TradeWallet.CanAfford((int)totalPrice))
                return TradeResult.Failed(TradeFailure.InsufficientMana, "Buyer cannot afford the trade.");
            if (!request.seller.TradeWallet.CanReceive((int)totalPrice))
                return TradeResult.Failed(TradeFailure.InsufficientManaCapacity, "Seller cannot receive the mana.");

            return TradeResult.Valid();
        }

        private static void TrimHistory()
        {
            while (completedTrades.Count > MaxCompletedTrades)
                completedTrades.RemoveAt(0);
        }
    }

    /// <summary>
    /// Complete proposal for one item-for-mana exchange.
    /// Unity setup: none. Create in code and pass to TradeService.TryExecute.
    /// </summary>
    public sealed class TradeRequest
    {
        public ITradeParticipant buyer;
        public ITradeParticipant seller;
        public IItem item;
        public int quantity;
        public int unitPrice;
        public string marketId;
        public string reason;
    }

    /// <summary>
    /// Result of a trade attempt, including a failure classification or committed transaction.
    /// Unity setup: none. Returned by TradeService.TryExecute.
    /// </summary>
    public sealed class TradeResult
    {
        public bool Success { get; private set; }
        public TradeFailure Failure { get; private set; }
        public string Message { get; private set; }
        public MarketTransaction Transaction { get; private set; }

        internal static TradeResult Valid() => new TradeResult { Success = true };

        internal static TradeResult Succeeded(MarketTransaction transaction) => new TradeResult
        {
            Success = true,
            Failure = TradeFailure.None,
            Message = string.Empty,
            Transaction = transaction,
        };

        internal static TradeResult Failed(TradeFailure failure, string message) => new TradeResult
        {
            Success = false,
            Failure = failure,
            Message = message ?? string.Empty,
        };
    }

    /// <summary>Stable reason a TradeService request failed. Unity setup: none.</summary>
    public enum TradeFailure
    {
        None,
        InvalidRequest,
        MissingParticipant,
        MissingParticipantId,
        SameParticipant,
        SharedOwnership,
        InvalidItem,
        InvalidQuantity,
        InvalidPrice,
        PriceOverflow,
        MissingInventory,
        MissingWallet,
        InsufficientStock,
        InsufficientInventorySpace,
        InsufficientMana,
        InsufficientManaCapacity,
        CommitFailed,
    }
}
