using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free loot quantity roll. Given a min/max range it returns an inclusive random amount,
    /// driven by a caller-supplied RNG so it is deterministic and unit-testable without a scene.
    ///
    /// Unity setup: none. Called by EnemyLootDrop.
    /// </summary>
    public static class LootTable
    {
        /// <summary>Rolls an inclusive quantity in [minQuantity, maxQuantity].</summary>
        public static int RollQuantity(int minQuantity, int maxQuantity, Random rng)
        {
            int min = Math.Max(0, minQuantity);
            int max = Math.Max(min, maxQuantity);

            if (max <= min || rng == null)
                return min;

            return rng.Next(min, max + 1);
        }
    }
}
