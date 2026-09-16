using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for NpcDashMeleeController. Plain C#, lives in Game.Data. The warning color is stored
    /// as RGBA floats and the arena bounds as x/y/width/height, since Game.Data cannot reference
    /// UnityEngine types.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcDashMeleeConfig field by
    /// NpcDashMeleeController.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcDashMeleeConfig
    {
        public float WarningDuration = 0.5f;
        public float WarningR = 1f;
        public float WarningG = 0.92f;
        public float WarningB = 0.016f;
        public float WarningA = 1f;
        public float ApproachSpeed = 2.8f;
        public float DashRange = 6f;
        public float DashSpeed = 14f;
        public float StoppingDistance = 1.1f;
        public float RecoveryDuration = 1.1f;
        public float ArenaX = 61f;
        public float ArenaY = -7f;
        public float ArenaWidth = 18f;
        public float ArenaHeight = 14f;
    }
}
