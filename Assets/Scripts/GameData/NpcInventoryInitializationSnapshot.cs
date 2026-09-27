namespace Game.Core
{
    /// <summary>
    /// Immutable capture of one NPC's starting-inventory initialization flag, for save and load.
    /// Plain value type, lives in Game.Data.
    ///
    /// Unity setup: none.
    /// </summary>
    public readonly struct NpcInventoryInitializationSnapshot
    {
        public readonly string NpcId;
        public readonly bool Initialized;

        public NpcInventoryInitializationSnapshot(string npcId, bool initialized)
        {
            NpcId = npcId;
            Initialized = initialized;
        }
    }
}
