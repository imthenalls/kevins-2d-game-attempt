using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free weighted behavior selection for an NPC. Given a list of behavior weights, picks the
    /// next index by weighted random roll. Holds only a seeded RNG, so it is deterministic and
    /// unit-testable without a scene. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Constructed by NpcBehaviorManager.
    /// </summary>
    public sealed class NpcBehaviorScheduler
    {
        private readonly Random rng;

        public NpcBehaviorScheduler(int seed)
        {
            rng = new Random(seed);
        }

        /// <summary>
        /// Weighted pick over <paramref name="weights"/>. Negative/zero weights are never chosen.
        /// Returns the chosen index, or -1 when the list is empty.
        /// </summary>
        public int PickNext(float[] weights)
        {
            if (weights == null || weights.Length == 0)
                return -1;

            double total = 0.0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0f)
                    total += weights[i];
            }

            if (total <= 0.0)
                return weights.Length - 1; // all weights invalid: fall back to the last behavior

            double roll = rng.NextDouble() * total;
            double cumulative = 0.0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0f)
                    cumulative += weights[i];
                if (roll < cumulative)
                    return i;
            }

            return weights.Length - 1;
        }
    }
}
