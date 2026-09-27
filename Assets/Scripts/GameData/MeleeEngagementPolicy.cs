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

        /// <summary>No valid target (absent or dead): do nothing and leave combat.</summary>
        Idle = 3,

        /// <summary>Target is valid but the NPC's behavior state forbids acting (Talking / Disabled).</summary>
        Blocked = 4,
    }

    /// <summary>
    /// Engine-free decision for a proximity-melee NPC. Given whether the target is alive, the NPC's
    /// behavior state, the distance to the target, and the attack/disengage tuning, it returns this
    /// tick's intent: Idle, Blocked, Disengage, Chase, or Attack. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Called by NpcProximityMelee3D / NpcProximityMeleeController, which keep the
    /// player lookup, pathing, velocity, and attack execution.
    /// </summary>
    public static class MeleeEngagementPolicy
    {
        public static MeleeEngagement Evaluate(
            float distance,
            float attackRange,
            float disengageRangeMultiplier,
            bool targetAlive,
            NpcBehaviorState behaviorState)
        {
            if (!targetAlive)
                return MeleeEngagement.Idle;

            if (behaviorState == NpcBehaviorState.Talking || behaviorState == NpcBehaviorState.Disabled)
                return MeleeEngagement.Blocked;

            if (attackRange <= 0f)
                return MeleeEngagement.Disengage;

            float disengageRange = attackRange * (disengageRangeMultiplier < 1f ? 1f : disengageRangeMultiplier);
            if (distance > disengageRange)
                return MeleeEngagement.Disengage;

            return distance > attackRange ? MeleeEngagement.Chase : MeleeEngagement.Attack;
        }
    }
}
