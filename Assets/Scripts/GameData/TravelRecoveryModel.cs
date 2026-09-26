using System;

namespace Game.Core
{
    /// <summary>Outcome of a travel-recovery tick.</summary>
    public enum TravelRecoveryDecision
    {
        /// <summary>Still making progress (or within the stall grace period).</summary>
        Moving = 0,

        /// <summary>The current waypoint was reached; tracking restarts.</summary>
        Arrived = 1,

        /// <summary>No progress for the stall timeout; recompute the route.</summary>
        Repath = 2,

        /// <summary>No progress and the repath budget is spent; cancel the trip.</summary>
        Abandon = 3,
    }

    /// <summary>
    /// Engine-free stall detection and recovery policy for an NPC travelling toward a destination.
    /// It tracks progress toward the current waypoint and, when progress stops, asks the caller to
    /// recompute the route a bounded number of times before abandoning the trip. Plain C#, lives in
    /// Game.Data and is unit-testable without a scene.
    ///
    /// Unity setup: none. Constructed by NpcSchedule3D / NpcWander3D from
    /// <see cref="NpcTravelRecoveryConfig"/>.
    /// </summary>
    public sealed class TravelRecoveryModel
    {
        // Matches the wander epsilon (0.02 world units) squared.
        private const float ProgressEpsilonSqr = 0.0004f;

        private readonly float stallTimeout;
        private readonly int maxRepaths;

        private float lastX, lastZ, stalledTime;
        private int repaths;

        public TravelRecoveryModel(float stallTimeout, int maxRepaths)
        {
            this.stallTimeout = stallTimeout < 0f ? 0f : stallTimeout;
            this.maxRepaths = maxRepaths < 0 ? 0 : maxRepaths;
        }

        /// <summary>How many times the route has been recalculated for the current trip.</summary>
        public int Repaths => repaths;

        /// <summary>Starts tracking at a position. Use when a fresh trip or brand-new route begins.</summary>
        public void Reset(float x, float z)
        {
            lastX = x;
            lastZ = z;
            stalledTime = 0f;
            repaths = 0;
        }

        /// <summary>
        /// Call once per tick. <paramref name="atWaypoint"/> is true when the current waypoint was
        /// reached. Returns what the caller should do next.
        /// </summary>
        public TravelRecoveryDecision Evaluate(float x, float z, float deltaSeconds, bool atWaypoint)
        {
            if (atWaypoint)
            {
                lastX = x;
                lastZ = z;
                stalledTime = 0f;
                return TravelRecoveryDecision.Arrived;
            }

            float movedX = x - lastX;
            float movedZ = z - lastZ;
            if (movedX * movedX + movedZ * movedZ >= ProgressEpsilonSqr)
            {
                lastX = x;
                lastZ = z;
                stalledTime = 0f;
                return TravelRecoveryDecision.Moving;
            }

            stalledTime += deltaSeconds;
            if (stalledTime < stallTimeout)
                return TravelRecoveryDecision.Moving;

            stalledTime = 0f;
            if (repaths < maxRepaths)
            {
                repaths++;
                return TravelRecoveryDecision.Repath;
            }

            return TravelRecoveryDecision.Abandon;
        }
    }
}
