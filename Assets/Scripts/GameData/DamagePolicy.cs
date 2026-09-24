using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free damage rules for a receiver: whether a hit is accepted and how raw damage is
    /// scaled. Plain C#, lives in Game.Data, so the combat damage policy is unit-testable without a
    /// scene.
    ///
    /// Unity setup: none. Called by CombatReceiver.
    /// </summary>
    public static class DamagePolicy
    {
        /// <summary>True when the receiver accepts damage at all.</summary>
        public static bool CanReceive(bool combatEnabled, bool invincible, bool alive) =>
            combatEnabled && !invincible && alive;

        /// <summary>
        /// Scales raw damage by the receiver's multiplier, rounded to nearest (ties to even) like
        /// Unity's Mathf.RoundToInt. Never negative.
        /// </summary>
        public static int Resolve(int rawAmount, float multiplier)
        {
            if (rawAmount <= 0)
                return 0;

            double scaled = rawAmount * (double)multiplier;
            if (scaled < 0.0)
                scaled = 0.0;

            return (int)Math.Round(scaled, MidpointRounding.ToEven);
        }

        /// <summary>
        /// True when an NPC of this type, on death, drops a loot pile, hides its body and raises the
        /// EnemyKilled event. Non-enemy NPCs simply die.
        /// </summary>
        public static bool DropsLootOnDeath(NpcType type) => type == NpcType.Enemy;
    }
}
