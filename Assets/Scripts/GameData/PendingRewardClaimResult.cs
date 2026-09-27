namespace Game.Core
{
    /// <summary>
    /// Summary of a <see cref="PendingRewardLedger.Claim"/> pass: how many items were delivered and
    /// how many entries were dropped because their item could not be resolved.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class PendingRewardClaimResult
    {
        /// <summary>Total quantity delivered across every pending entry.</summary>
        public int Delivered;

        /// <summary>Number of entries dropped for an unresolvable item id.</summary>
        public int Dropped;

        /// <summary>True when anything was delivered or dropped.</summary>
        public bool ChangedAnything => Delivered > 0 || Dropped > 0;
    }
}
