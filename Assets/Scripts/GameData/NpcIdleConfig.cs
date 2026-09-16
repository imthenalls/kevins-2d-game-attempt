using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for NpcIdleBehavior. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcIdleConfig field by NpcIdleBehavior.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcIdleConfig
    {
        public float Weight = 50f;
        public float MinDuration = 2f;
        public float MaxDuration = 5f;
    }
}
