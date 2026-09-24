using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for NpcWanderBehavior. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcWanderConfig field by NpcWanderBehavior.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcWanderConfig
    {
        public float WanderRadius = 3f;
        public float ArrivalThreshold = 0.2f;
        public float WallLookAhead = 0.3f;

        /// <summary>Idle pause between destinations, in seconds (3D wanderer).</summary>
        public float IdleSeconds = 1.5f;

        /// <summary>
        /// When true and the NPC has an NpcPathfinder, wander picks a reachable destination and
        /// follows a grid path (falling back to straight-line movement when no path exists).
        /// </summary>
        public bool UsePathfinding = true;
    }
}
