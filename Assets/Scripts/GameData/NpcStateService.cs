namespace Game.Core
{
    /// <summary>
    /// Application service for NPC state. Gameplay changes (damage, heal, move) go through these
    /// commands instead of being applied directly by a view or MonoBehaviour.
    ///
    /// Unity setup: none. Constructed by GameSession with its NpcStateRepository.
    ///
    /// Runtime API: Register, ApplyDamage, Heal, MoveToCell, TryCapture, Apply.
    /// </summary>
    public sealed class NpcStateService
    {
        private readonly NpcStateRepository repository;

        public NpcStateService(NpcStateRepository repository)
        {
            this.repository = repository ?? throw new System.ArgumentNullException(nameof(repository));
        }

        public NpcState Register(string npcId, int maxHp, int hp, int cellX, int cellY) =>
            repository.GetOrCreate(npcId, maxHp, hp, cellX, cellY);

        public bool TryGet(string npcId, out NpcState state) => repository.TryGet(npcId, out state);

        public int ApplyDamage(string npcId, int amount) =>
            repository.TryGet(npcId, out NpcState state) ? state.ApplyDamage(amount) : 0;

        public int Heal(string npcId, int amount) =>
            repository.TryGet(npcId, out NpcState state) ? state.Heal(amount) : 0;

        public void MoveToCell(string npcId, int cellX, int cellY)
        {
            if (repository.TryGet(npcId, out NpcState state))
                state.MoveToCell(cellX, cellY);
        }

        /// <summary>Captures the model for saving. Returns false when the id is unknown.</summary>
        public bool TryCapture(string npcId, out NpcStateSnapshot snapshot)
        {
            if (repository.TryGet(npcId, out NpcState state))
            {
                snapshot = new NpcStateSnapshot(state.NpcId, state.Hp, state.MaxHp, state.CellX, state.CellY);
                return true;
            }

            snapshot = default;
            return false;
        }

        /// <summary>Restores a saved snapshot into the model (used by load).</summary>
        public NpcState Apply(NpcStateSnapshot snapshot)
        {
            NpcState state = repository.GetOrCreate(
                snapshot.NpcId, snapshot.MaxHp, snapshot.Hp, snapshot.CellX, snapshot.CellY);
            state.Restore(snapshot.Hp, snapshot.MaxHp, snapshot.CellX, snapshot.CellY);
            return state;
        }
    }
}
