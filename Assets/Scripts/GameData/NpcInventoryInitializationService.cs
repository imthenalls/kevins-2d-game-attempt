using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Application service that owns the one-time starting-inventory decision for every NPC. The
    /// session keeps the authoritative "already initialized" set, so a legitimately emptied inventory
    /// is not reseeded on a scene reload and the state can be saved and restored. Mirrors
    /// <see cref="NpcMemoryService"/>.
    ///
    /// Unity setup: none. Constructed by GameSession; NpcInventoryDatabase forwards here.
    ///
    /// Runtime API: IsInitialized, TryClaimSeed, MarkInitialized, ResetSession, TryCapture, Apply.
    /// </summary>
    public sealed class NpcInventoryInitializationService
    {
        private readonly Dictionary<string, NpcInventoryInitializationModel> byId =
            new Dictionary<string, NpcInventoryInitializationModel>(StringComparer.OrdinalIgnoreCase);

        /// <summary>True when the NPC has already been seeded or restored this session.</summary>
        public bool IsInitialized(string npcId) =>
            !string.IsNullOrWhiteSpace(npcId) &&
            byId.TryGetValue(npcId, out NpcInventoryInitializationModel model) &&
            model.Initialized;

        /// <summary>
        /// Claims the one-time starting-inventory seed for <paramref name="npcId"/>. Returns true only
        /// when the NPC has never been initialized, marking the session (and the inventory instance)
        /// so the starting items are applied at most once. The decision uses explicit initialization
        /// state, not the item count, so an emptied inventory is never reseeded.
        /// </summary>
        public bool TryClaimSeed(string npcId, InventoryModel inventory)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return false;

            if (IsInitialized(npcId))
                return false;

            // A save-restored inventory is authoritative; record it and skip the seed.
            if (inventory != null && inventory.IsInitialized)
            {
                Register(npcId).MarkInitialized();
                return false;
            }

            Register(npcId).MarkInitialized();
            inventory?.MarkInitialized();
            return true;
        }

        /// <summary>
        /// Records an NPC as initialized without seeding (used when a save restores its inventory).
        /// </summary>
        public void MarkInitialized(string npcId, InventoryModel inventory = null)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return;

            Register(npcId).MarkInitialized();
            inventory?.MarkInitialized();
        }

        /// <summary>Clears per-session initialization so a new-game flow seeds starting inventories.</summary>
        public void ResetSession() => byId.Clear();

        /// <summary>Captures one NPC's flag for saving. Returns false when the id is unknown.</summary>
        public bool TryCapture(string npcId, out NpcInventoryInitializationSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(npcId) &&
                byId.TryGetValue(npcId, out NpcInventoryInitializationModel model))
            {
                snapshot = new NpcInventoryInitializationSnapshot(npcId, model.Initialized);
                return true;
            }

            snapshot = default;
            return false;
        }

        /// <summary>Restores a saved flag into the model (used by load).</summary>
        public void Apply(NpcInventoryInitializationSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.NpcId))
                return;

            NpcInventoryInitializationModel model = Register(snapshot.NpcId);
            if (snapshot.Initialized)
                model.MarkInitialized();
            else
                model.Reset();
        }

        private NpcInventoryInitializationModel Register(string npcId)
        {
            if (!byId.TryGetValue(npcId, out NpcInventoryInitializationModel model))
            {
                model = new NpcInventoryInitializationModel();
                byId[npcId] = model;
            }

            return model;
        }
    }
}
