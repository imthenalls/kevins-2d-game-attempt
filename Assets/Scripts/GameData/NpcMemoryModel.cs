using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>One gate an NPC found locked, with the key id it required. Serializable for saves.</summary>
    [Serializable]
    public sealed class NpcGateMemory
    {
        public string gateId;
        public string requiredKeyId;

        public NpcGateMemory()
        {
        }

        public NpcGateMemory(string gateId, string requiredKeyId)
        {
            this.gateId = gateId;
            this.requiredKeyId = requiredKeyId;
        }
    }

    /// <summary>
    /// Engine-free knowledge model for one NPC. It owns the rules for remembering a locked gate,
    /// deciding whether to skip it, and forgetting it once the required key is held. Knowledge is
    /// keyed by stable gate id rather than a scene reference, so it can be saved and tested without a
    /// scene.
    ///
    /// Unity setup: none. Owned by GameSession via NpcMemoryRepository; the NpcMemory MonoBehaviour is
    /// a thin facade that resolves the SlidingDoor and key holder into ids and booleans.
    ///
    /// Runtime API: RememberLockedGate, HasLockedMemory, TryGetRequiredKey, ShouldSkipGate,
    /// ForgetGate, Clear, Capture, Restore, Count.
    /// </summary>
    public sealed class NpcMemoryModel
    {
        private readonly Dictionary<string, string> lockedGates =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Number of gates currently remembered as locked.</summary>
        public int Count => lockedGates.Count;

        /// <summary>Records that a gate is locked and needs the given key. False for blank input.</summary>
        public bool RememberLockedGate(string gateId, string requiredKeyId)
        {
            if (string.IsNullOrWhiteSpace(gateId) || string.IsNullOrWhiteSpace(requiredKeyId))
                return false;

            lockedGates[gateId] = requiredKeyId;
            return true;
        }

        public bool HasLockedMemory(string gateId) =>
            !string.IsNullOrWhiteSpace(gateId) && lockedGates.ContainsKey(gateId);

        public bool TryGetRequiredKey(string gateId, out string requiredKeyId)
        {
            requiredKeyId = null;
            return !string.IsNullOrWhiteSpace(gateId) &&
                   lockedGates.TryGetValue(gateId, out requiredKeyId);
        }

        /// <summary>
        /// True when the NPC should skip a gate it remembers as locked but still cannot open. Forgets
        /// the memory and returns false once the holder owns the required key.
        /// </summary>
        public bool ShouldSkipGate(string gateId, bool holdsRequiredKey)
        {
            if (!TryGetRequiredKey(gateId, out _))
                return false;

            if (holdsRequiredKey)
            {
                ForgetGate(gateId);
                return false;
            }

            return true;
        }

        public bool ForgetGate(string gateId) =>
            !string.IsNullOrWhiteSpace(gateId) && lockedGates.Remove(gateId);

        /// <summary>Forgets every gate this NPC remembers. Replaces any older local-only clear.</summary>
        public void Clear() => lockedGates.Clear();

        /// <summary>Appends every remembered gate into <paramref name="into"/> for saving.</summary>
        public void Capture(List<NpcGateMemory> into)
        {
            if (into == null)
                return;

            foreach (KeyValuePair<string, string> pair in lockedGates)
                into.Add(new NpcGateMemory(pair.Key, pair.Value));
        }

        /// <summary>Adds remembered gates from a saved snapshot.</summary>
        public void Restore(IEnumerable<NpcGateMemory> gates)
        {
            if (gates == null)
                return;

            foreach (NpcGateMemory gate in gates)
            {
                if (gate != null)
                    RememberLockedGate(gate.gateId, gate.requiredKeyId);
            }
        }
    }
}
