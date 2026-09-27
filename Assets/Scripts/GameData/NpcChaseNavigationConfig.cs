using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for the chase navigation policy: when to recompute a path, when to fall back to a
    /// direct step, and how close counts as reaching a waypoint. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcChaseNavigationConfig field by
    /// NpcChaseNavigator and consumed by <see cref="NpcChaseNavigationPolicy"/>.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcChaseNavigationConfig
    {
        /// <summary>Beyond this distance the enemy steps directly; chase is meant to be local.</summary>
        public float MaxPathDistance = 25f;

        /// <summary>How far the target must move before the path is recomputed immediately.</summary>
        public float RepathTargetMoved = 1.5f;

        /// <summary>Never recompute more often than this, even if the caller asks for less.</summary>
        public float MinRepathInterval = 0.05f;

        /// <summary>Planar distance at which a waypoint is considered reached.</summary>
        public float WaypointReached = 0.35f;
    }
}
