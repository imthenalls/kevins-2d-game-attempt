using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Plain snapshot of one NPC's remembered locked gates, for save and load.</summary>
    public sealed class NpcMemorySnapshot
    {
        public string NpcId { get; }
        public List<NpcGateMemory> Gates { get; }

        public NpcMemorySnapshot(string npcId, List<NpcGateMemory> gates)
        {
            NpcId = npcId;
            Gates = gates ?? new List<NpcGateMemory>();
        }
    }
}
