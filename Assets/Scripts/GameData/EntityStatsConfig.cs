using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative HP/MP configuration for an entity. Plain C# (no UnityEngine), lives in
    /// Game.Data, and is the single source EntityStats reads instead of its own serialized
    /// integers. Runtime Configure calls write back into this object.
    ///
    /// Unity setup: none. Held as a [SerializeField] EntityStatsConfig field by EntityStats.
    ///
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class EntityStatsConfig
    {
        public int MaxHp = 100;
        public int StartingHp = 100;
        public int MaxMp = 50;
        public int StartingMp = 50;
    }
}
