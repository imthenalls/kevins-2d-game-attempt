using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for how a travelling NPC reacts to a lack of progress. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcTravelRecoveryConfig field by the movement
    /// components (NpcSchedule3D / NpcWander3D) and consumed by <see cref="TravelRecoveryModel"/>.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcTravelRecoveryConfig
    {
        /// <summary>Seconds without progress before the route is recalculated.</summary>
        public float StallTimeout = 0.75f;

        /// <summary>How many times the route may be recalculated before the trip is abandoned.</summary>
        public int MaxRepaths = 2;

        /// <summary>Seconds to wait (while wandering) before retrying an abandoned home trip.</summary>
        public float RetrySeconds = 5f;
    }
}
