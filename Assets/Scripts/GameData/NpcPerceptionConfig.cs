using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for an NPC's proximity scan. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcPerceptionConfig field by NpcPerception and
    /// clamped by that component's OnValidate.
    ///
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcPerceptionConfig
    {
        /// <summary>Radius of the shared proximity scan, in world units.</summary>
        public float ScanRadius = 8f;
    }
}
