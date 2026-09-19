using System;
namespace Game.Core
{
    /// <summary>
    /// Serializable audit record for one completed market exchange.
    /// Unity setup: none. TradeService creates it and SaveManager persists it.
    /// </summary>
    [Serializable]
    public sealed class MarketTransaction
    {
        public string tradeId;
        public string utcTimestamp;
        public string buyerParticipantId;
        public string sellerParticipantId;
        public string itemId;
        public int quantity;
        public int unitPrice;
        public int totalPrice;
        public string marketId;
        public string reason;

        public MarketTransaction Clone()
        {
            return new MarketTransaction
            {
                tradeId = tradeId,
                utcTimestamp = utcTimestamp,
                buyerParticipantId = buyerParticipantId,
                sellerParticipantId = sellerParticipantId,
                itemId = itemId,
                quantity = quantity,
                unitPrice = unitPrice,
                totalPrice = totalPrice,
                marketId = marketId,
                reason = reason,
            };
        }
    }
}
