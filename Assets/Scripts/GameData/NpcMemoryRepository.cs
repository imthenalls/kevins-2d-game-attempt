using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Stores one <see cref="NpcMemoryModel"/> per stable npcId. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Owned by GameSession.
    ///
    /// Runtime API: GetOrCreate, TryGet.
    /// </summary>
    public sealed class NpcMemoryRepository
    {
        private readonly Dictionary<string, NpcMemoryModel> byId =
            new Dictionary<string, NpcMemoryModel>(StringComparer.OrdinalIgnoreCase);

        public NpcMemoryModel GetOrCreate(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException("A stable npcId is required.", nameof(npcId));

            if (!byId.TryGetValue(npcId, out NpcMemoryModel model))
            {
                model = new NpcMemoryModel();
                byId[npcId] = model;
            }

            return model;
        }

        public bool TryGet(string npcId, out NpcMemoryModel model)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                model = null;
                return false;
            }

            return byId.TryGetValue(npcId, out model);
        }
    }
}
