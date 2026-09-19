using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Serializable save entry for one active quest: its id, active node ids, and objective counts.
    /// Engine-free save shape; QuestManager produces and consumes these.
    ///
    /// Unity setup: none.
    /// </summary>
    [System.Serializable]
    public class QuestSaveEntry
    {
        public string questId;
        public List<string> activeNodeIds;
        public List<ObjectiveCountEntry> objectiveCounts;
    }

    /// <summary>
    /// Serializable objective progress entry (objective id + current count).
    /// Unity setup: none.
    /// </summary>
    [System.Serializable]
    public class ObjectiveCountEntry
    {
        public string objectiveId;
        public int count;
    }
}
