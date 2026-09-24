using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Stores one <see cref="NpcScheduleState"/> per stable npcId. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Owned by GameSession.
    ///
    /// Runtime API: GetOrCreate, TryGet.
    /// </summary>
    public sealed class NpcScheduleRepository
    {
        private readonly Dictionary<string, NpcScheduleState> byId =
            new Dictionary<string, NpcScheduleState>(StringComparer.OrdinalIgnoreCase);

        public NpcScheduleState GetOrCreate(string npcId, NpcSchedulePhase phase, float secondsRemaining)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException("A stable npcId is required.", nameof(npcId));

            if (!byId.TryGetValue(npcId, out NpcScheduleState state))
            {
                state = new NpcScheduleState(npcId, phase, secondsRemaining);
                byId[npcId] = state;
            }

            return state;
        }

        public bool TryGet(string npcId, out NpcScheduleState state)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                state = null;
                return false;
            }

            return byId.TryGetValue(npcId, out state);
        }
    }
}
