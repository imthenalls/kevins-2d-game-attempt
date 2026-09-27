using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for a proximity-melee NPC: how fast it closes on the target, how far past attack range
    /// it keeps chasing before giving up, and how often it recomputes its chase path. Plain C#,
    /// lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcMeleeEngagementConfig field by
    /// NpcProximityMelee3D; NpcProximityMeleeController (2D) has no chase and uses the multiplier 1.
    ///
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcMeleeEngagementConfig
    {
        /// <summary>Movement speed while closing on the target.</summary>
        public float ChaseSpeed = 2.6f;

        /// <summary>Give up and resume normal behavior once the target is this many attack ranges away.</summary>
        public float DisengageRangeMultiplier = 4f;

        /// <summary>How often to recompute the chase path, in seconds.</summary>
        public float RepathInterval = 0.4f;
    }
}
