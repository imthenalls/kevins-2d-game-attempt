using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for the local-avoidance steering used by travelling NPCs (NpcLocalAvoidance in the
    /// Shell). Plain C#, lives in Game.Data so the values are not Presentation fields.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcLocalAvoidanceConfig field by the NPC movement
    /// facades (NpcWander3D / NpcSchedule3D).
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcLocalAvoidanceConfig
    {
        /// <summary>Distance at which nearby NPCs start pushing this one aside.</summary>
        public float NeighborSeparation = 0.9f;

        /// <summary>How strongly nearby NPCs deflect movement (0 = ignore neighbors).</summary>
        public float NeighborSteerStrength = 1.4f;
    }
}
