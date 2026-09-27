using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// One candidate for nearest-target selection: a gameplay-plane position plus whether the
    /// candidate is eligible (for example a closed gate, or a live component). The Unity adapters
    /// build these from physics overlaps; the ranking rule itself is engine-free.
    /// </summary>
    public readonly struct NpcTargetCandidate
    {
        public readonly float X;
        public readonly float Y;
        public readonly bool Valid;

        public NpcTargetCandidate(float x, float y, bool valid)
        {
            X = x;
            Y = y;
            Valid = valid;
        }
    }

    /// <summary>
    /// Engine-free nearest-target ranking. The Unity adapters only sample physics and resolve
    /// components; the "nearest eligible candidate within range" rule lives here so it can be unit
    /// tested without a scene. Ineligible candidates are skipped, and ties resolve to the later
    /// candidate (matching the original selection order).
    ///
    /// Unity setup: none.
    /// </summary>
    public static class NpcTargetSelection
    {
        /// <summary>
        /// Whether a gate is a valid target: it must exist and be closed. The Unity adapter reports
        /// the two facts; this rule (an open gate is not worth walking to) lives here so it can be
        /// unit-tested without a scene.
        /// </summary>
        public static bool IsGateEligible(bool gateExists, bool gateOpen) => gateExists && !gateOpen;

        /// <summary>
        /// Index of the nearest eligible candidate within <paramref name="maxDistance"/> of
        /// (<paramref name="originX"/>, <paramref name="originY"/>), or -1 when none qualifies.
        /// </summary>
        public static int NearestIndex(
            float originX,
            float originY,
            IList<NpcTargetCandidate> candidates,
            float maxDistance)
        {
            if (candidates == null || candidates.Count == 0)
                return -1;

            float nearestSqr = maxDistance * maxDistance;
            int nearest = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                NpcTargetCandidate candidate = candidates[i];
                if (!candidate.Valid)
                    continue;

                float dx = candidate.X - originX;
                float dy = candidate.Y - originY;
                float sqr = dx * dx + dy * dy;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = i;
                }
            }

            return nearest;
        }
    }
}
