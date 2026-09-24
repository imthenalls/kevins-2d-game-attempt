using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for an NPC home schedule. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcScheduleConfig field by NpcSchedule3D.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcScheduleConfig
    {
        /// <summary>Seconds spent out and about before heading home.</summary>
        public float AwaySeconds = 25f;

        /// <summary>Seconds spent inside the home before leaving again.</summary>
        public float HomeSeconds = 18f;
    }
}
