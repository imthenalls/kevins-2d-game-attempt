using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for NpcUseDoorBehavior. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcUseDoorConfig field by NpcUseDoorBehavior.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcUseDoorConfig
    {
        public float DetectionRadius = 6f;
        public float WaypointThreshold = 0.2f;
        public float PassThroughDistance = 2f;
    }
}
