namespace Game.Core
{
    /// <summary>What a proximity-melee NPC should do this tick.</summary>
    public enum MeleeEngagement
    {
        /// <summary>Too far: drop combat and resume normal behavior.</summary>
        Disengage = 0,

        /// <summary>Close the distance to the target.</summary>
        Chase = 1,

        /// <summary>In range: stop and attack.</summary>
        Attack = 2,
    }

    /// <summary>
    /// Engine-free decision for a proximity-melee NPC: given the distance to the target, the attack
    /// range, and how far past the range the NPC keeps chasing before giving up, decide whether to
    /// disengage, chase, or attack. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Called by NpcProximityMelee3D / NpcProximityMeleeController.
    /// </summary>
    public static class MeleeEngagementPolicy
    {
        public static MeleeEngagement Evaluate(float distance, float attackRange, float disengageRangeMultiplier)
        {
            if (attackRange <= 0f)
                return MeleeEngagement.Disengage;

            float disengageRange = attackRange * (disengageRangeMultiplier < 1f ? 1f : disengageRangeMultiplier);
            if (distance > disengageRange)
                return MeleeEngagement.Disengage;

            return distance > attackRange ? MeleeEngagement.Chase : MeleeEngagement.Attack;
        }
    }
}
