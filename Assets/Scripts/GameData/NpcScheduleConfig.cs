using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for an NPC home schedule and its trip. Plain C#, lives in Game.Data. Durations and the
    /// door-entry/waypoint distances are schedule decisions, so they live here rather than as
    /// Presentation fields on the MonoBehaviour.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcScheduleConfig field by NpcSchedule3D.
    /// Runtime API: plain public fields and InitialAwaySeconds(seed).
    /// </summary>
    [Serializable]
    public class NpcScheduleConfig
    {
        /// <summary>Seconds spent out and about before heading home.</summary>
        public float AwaySeconds = 25f;

        /// <summary>Seconds spent inside the home before leaving again.</summary>
        public float HomeSeconds = 18f;

        /// <summary>Seconds to wait (while wandering) before retrying a cancelled trip.</summary>
        public float RetrySeconds = 5f;

        /// <summary>Lower bound of the randomized first Away leg, as a fraction of AwaySeconds.</summary>
        public float InitialAwayMinFraction = 0.3f;

        /// <summary>Distance to the home approach at which the NPC goes inside.</summary>
        public float HomeDoorEnterRadius = 1.5f;

        /// <summary>Distance at which a route waypoint counts as reached.</summary>
        public float WaypointThreshold = 0.35f;

        /// <summary>
        /// Deterministic randomized first-Away duration in
        /// [AwaySeconds * InitialAwayMinFraction, AwaySeconds]. Lives in Core so the schedule does not
        /// depend on Unity's random API.
        /// </summary>
        public float InitialAwaySeconds(int seed)
        {
            float min = Math.Max(0f, Math.Min(InitialAwayMinFraction, 1f));
            double t = new Random(seed).NextDouble();
            float fraction = (float)(min + t * (1.0 - min));
            return AwaySeconds * fraction;
        }
    }
}
