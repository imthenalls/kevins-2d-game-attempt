using System;

namespace Game.Core
{
    /// <summary>
    /// Serializable record of a quest reward that could not be fully delivered (inventory full).
    /// The player can claim the remaining quantity later. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Owned by the presentation-side PendingRewardManager and serialized into
    /// the save file.
    /// </summary>
    [Serializable]
    public class PendingRewardEntry
    {
        /// <summary>Stable unique id for this reward, used to deduplicate claims.</summary>
        public string rewardId;

        /// <summary>The quest that granted the reward.</summary>
        public string questId;

        /// <summary>The item id still owed to the player.</summary>
        public string itemId;

        /// <summary>Quantity not yet delivered.</summary>
        public int remaining;
    }
}
