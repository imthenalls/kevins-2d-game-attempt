namespace Game.Core
{
    /// <summary>
    /// Immutable snapshot of an NPC's saveable state: identity, health, and logical map cell.
    ///
    /// Unity setup: none — this is a plain C# value type. It is the boundary type used between
    /// the pure model and the Unity save system, so save data never holds scene references.
    /// </summary>
    public readonly struct NpcStateSnapshot
    {
        public readonly string NpcId;
        public readonly int Hp;
        public readonly int MaxHp;
        public readonly int CellX;
        public readonly int CellY;

        public NpcStateSnapshot(string npcId, int hp, int maxHp, int cellX, int cellY)
        {
            NpcId = npcId;
            Hp = hp;
            MaxHp = maxHp;
            CellX = cellX;
            CellY = cellY;
        }
    }
}
