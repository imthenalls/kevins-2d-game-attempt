using System;

namespace Game.Core
{
    /// <summary>
    /// Shared tuning for all NPC behaviors (base class values). Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcBehaviorConfig field by NpcBehaviorBase.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcBehaviorConfig
    {
        public float Weight = 50f;
        public float MoveSpeed = 2f;
        public float StallTimeout = 0.5f;
    }
}
