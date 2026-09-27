namespace Game.Core
{
    /// <summary>
    /// Application service for NPC schedule state. Gameplay transitions run on the model itself
    /// (<see cref="NpcScheduleState.Tick"/>/<see cref="NpcScheduleState.Handle"/>); this service owns
    /// registration and the save/load round trip so the MonoBehaviour never owns the phase or timer.
    ///
    /// Unity setup: none. Constructed by GameSession with its NpcScheduleRepository.
    ///
    /// Runtime API: Register, TryGet, TryCapture, Apply.
    /// </summary>
    public sealed class NpcScheduleService
    {
        private readonly NpcScheduleRepository repository;

        public NpcScheduleService(NpcScheduleRepository repository)
        {
            this.repository = repository ?? throw new System.ArgumentNullException(nameof(repository));
        }

        public NpcScheduleState Register(string npcId, NpcSchedulePhase phase, float secondsRemaining) =>
            repository.GetOrCreate(npcId, phase, secondsRemaining);

        public bool TryGet(string npcId, out NpcScheduleState state) => repository.TryGet(npcId, out state);

        /// <summary>Captures the model for saving. Returns false when the id is unknown.</summary>
        public bool TryCapture(string npcId, out NpcScheduleSnapshot snapshot)
        {
            if (repository.TryGet(npcId, out NpcScheduleState state))
            {
                snapshot = new NpcScheduleSnapshot(state.NpcId, state.Phase, state.SecondsRemaining);
                return true;
            }

            snapshot = default;
            return false;
        }

        /// <summary>Restores a saved snapshot into the model (used by load).</summary>
        public NpcScheduleState Apply(NpcScheduleSnapshot snapshot)
        {
            NpcScheduleState state = repository.GetOrCreate(snapshot.NpcId, snapshot.Phase, snapshot.SecondsRemaining);
            state.Restore(snapshot.Phase, snapshot.SecondsRemaining);
            return state;
        }
    }
}
