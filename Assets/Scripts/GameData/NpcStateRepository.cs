using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Owns the NPC state models, keyed by stable npcId. Part of the GameSession, so the models
    /// outlive any Unity view that renders them.
    ///
    /// Unity setup: none. Created by GameSession; accessed through NpcStateService.
    ///
    /// Runtime API: GetOrCreate, TryGet, Get, Remove, All.
    /// </summary>
    public sealed class NpcStateRepository
    {
        private readonly Dictionary<string, NpcState> byId =
            new Dictionary<string, NpcState>(StringComparer.OrdinalIgnoreCase);

        public NpcState GetOrCreate(string npcId, int maxHp, int hp, int cellX, int cellY)
        {
            if (byId.TryGetValue(npcId, out NpcState existing))
                return existing;

            var created = new NpcState(npcId, maxHp, hp, cellX, cellY);
            byId[npcId] = created;
            return created;
        }

        public bool TryGet(string npcId, out NpcState state) => byId.TryGetValue(npcId, out state);

        public NpcState Get(string npcId) => byId.TryGetValue(npcId, out NpcState state) ? state : null;

        public bool Remove(string npcId) => byId.Remove(npcId);

        public List<NpcState> All() => new List<NpcState>(byId.Values);
    }
}
