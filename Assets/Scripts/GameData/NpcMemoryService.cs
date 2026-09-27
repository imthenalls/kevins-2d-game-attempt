using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Application service for NPC knowledge. Gameplay reads and writes go through it so the
    /// MonoBehaviour never owns the rules or the authoritative state.
    ///
    /// Unity setup: none. Constructed by GameSession with its NpcMemoryRepository.
    ///
    /// Runtime API: Register, TryGet, Remember, HasLocked, ShouldSkip, Forget, Clear, TryCapture, Apply.
    /// </summary>
    public sealed class NpcMemoryService
    {
        private readonly NpcMemoryRepository repository;

        public NpcMemoryService(NpcMemoryRepository repository)
        {
            this.repository = repository ?? throw new System.ArgumentNullException(nameof(repository));
        }

        public NpcMemoryModel Register(string npcId) => repository.GetOrCreate(npcId);

        public bool TryGet(string npcId, out NpcMemoryModel model) => repository.TryGet(npcId, out model);

        public bool Remember(string npcId, string gateId, string requiredKeyId) =>
            Register(npcId).RememberLockedGate(gateId, requiredKeyId);

        public bool HasLocked(string npcId, string gateId) =>
            TryGet(npcId, out NpcMemoryModel model) && model.HasLockedMemory(gateId);

        public bool ShouldSkip(string npcId, string gateId, bool holdsRequiredKey) =>
            TryGet(npcId, out NpcMemoryModel model) && model.ShouldSkipGate(gateId, holdsRequiredKey);

        public bool Forget(string npcId, string gateId) =>
            TryGet(npcId, out NpcMemoryModel model) && model.ForgetGate(gateId);

        public void Clear(string npcId)
        {
            if (TryGet(npcId, out NpcMemoryModel model))
                model.Clear();
        }

        /// <summary>Captures one NPC's knowledge for saving. Returns false when the id is unknown.</summary>
        public bool TryCapture(string npcId, out NpcMemorySnapshot snapshot)
        {
            if (TryGet(npcId, out NpcMemoryModel model))
            {
                var gates = new List<NpcGateMemory>();
                model.Capture(gates);
                snapshot = new NpcMemorySnapshot(npcId, gates);
                return true;
            }

            snapshot = null;
            return false;
        }

        /// <summary>Restores saved knowledge into the model (used by load).</summary>
        public void Apply(NpcMemorySnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.NpcId))
                return;

            Register(snapshot.NpcId).Restore(snapshot.Gates);
        }
    }
}
