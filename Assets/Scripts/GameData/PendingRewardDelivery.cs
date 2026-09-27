namespace Game.Core
{
    /// <summary>
    /// Outcome of one delivery attempt made by the presentation adapter while claiming a pending
    /// reward: how much was actually accepted by the inventory, and whether the item could be
    /// resolved at all. The adapter owns item lookup; the ledger owns what happens next.
    ///
    /// Unity setup: none.
    /// </summary>
    public struct PendingRewardDelivery
    {
        /// <summary>Quantity the inventory accepted (clamped to the requested amount by the ledger).</summary>
        public int Delivered;

        /// <summary>False when the item id could not be resolved and the entry should be dropped.</summary>
        public bool ItemKnown;

        public static PendingRewardDelivery Accepted(int delivered)
            => new PendingRewardDelivery { Delivered = delivered, ItemKnown = true };

        public static PendingRewardDelivery Unknown()
            => new PendingRewardDelivery { Delivered = 0, ItemKnown = false };
    }
}
