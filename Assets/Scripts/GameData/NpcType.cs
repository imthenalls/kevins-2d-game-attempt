namespace Game.Core
{
    /// <summary>
    /// The kind of an NPC. Owned by the Core so rules (enemy death/loot, trading, dialogue, trainer)
    /// can be expressed engine-free and shared by 2D and 3D adapters.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum NpcType
    {
        Generic,
        QuestGiver,
        Vendor,
        Trainer,
        Enemy
    }
}
