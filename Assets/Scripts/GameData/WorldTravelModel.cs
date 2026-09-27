using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free authority for world travel and world-scoped progression: the active
    /// <see cref="WorldLayer"/>, each world's remembered logical return position, the abilities
    /// unlocked per world, and the shared player wallet state carried between avatars.
    ///
    /// Owned by <see cref="GameSession"/> so the values outlive scene loads and avatar
    /// destroy/recreate. The Unity <c>WorldTravelState</c> adapter performs scene loading, Grid
    /// conversion, and character activation, and forwards the resulting values here.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class WorldTravelModel
    {
        public event Action<WorldLayer> WorldChanged;
        public event Action<WorldLayer, string> AbilityUnlocked;

        public WorldLayer CurrentWorld { get; private set; } = WorldLayer.WorldA;

        private readonly Dictionary<WorldLayer, RememberedWorldPosition> positions = new();
        private readonly Dictionary<WorldLayer, HashSet<string>> unlockedAbilities = new();

        /// <summary>True once a wallet snapshot has been captured or loaded.</summary>
        public bool HasSharedPlayerState { get; private set; }

        /// <summary>The most recently captured/loaded shared wallet snapshot, or null.</summary>
        public WalletSaveData SharedWallet { get; private set; }

        /// <summary>
        /// Switches the active world. Returns true when it changed; raises <see cref="WorldChanged"/>
        /// only on an actual change.
        /// </summary>
        public bool SetCurrentWorld(WorldLayer world)
        {
            if (CurrentWorld == world) return false;

            CurrentWorld = world;
            WorldChanged?.Invoke(world);
            return true;
        }

        public void SetPosition(WorldLayer world, RememberedWorldPosition position)
        {
            if (position == null) return;
            positions[world] = position;
        }

        public bool TryGetPosition(WorldLayer world, out RememberedWorldPosition position)
            => positions.TryGetValue(world, out position);

        public void ClearPositions() => positions.Clear();

        public bool HasAbility(WorldLayer world, string abilityId)
        {
            string id = NormalizeAbilityId(abilityId);
            return id.Length > 0 &&
                   unlockedAbilities.TryGetValue(world, out HashSet<string> abilities) &&
                   abilities.Contains(id);
        }

        /// <summary>Unlocks an ability and raises <see cref="AbilityUnlocked"/> when newly added.</summary>
        public bool UnlockAbility(WorldLayer world, string abilityId)
            => AddAbility(world, abilityId, notify: true);

        /// <summary>
        /// Grants an ability without notification (used to seed starting abilities). Raises nothing.
        /// </summary>
        public bool GrantAbility(WorldLayer world, string abilityId)
            => AddAbility(world, abilityId, notify: false);

        public void WriteAbilities(List<WorldAbilitySaveEntry> destination)
        {
            if (destination == null) return;
            destination.Clear();

            foreach (var pair in unlockedAbilities)
            {
                foreach (string abilityId in pair.Value)
                {
                    destination.Add(new WorldAbilitySaveEntry
                    {
                        world = pair.Key.ToString(),
                        abilityId = abilityId,
                    });
                }
            }
        }

        public void LoadAbilities(List<WorldAbilitySaveEntry> savedAbilities)
        {
            unlockedAbilities.Clear();
            if (savedAbilities == null) return;

            for (int i = 0; i < savedAbilities.Count; i++)
            {
                WorldAbilitySaveEntry entry = savedAbilities[i];
                if (entry != null && Enum.TryParse(entry.world, out WorldLayer world))
                    AddAbility(world, entry.abilityId, notify: false);
            }
        }

        public void WritePositions(List<WorldPositionSaveEntry> destination)
        {
            if (destination == null) return;
            destination.Clear();

            foreach (var pair in positions)
            {
                var entry = new WorldPositionSaveEntry { world = pair.Key.ToString() };
                pair.Value.WriteTo(entry);
                destination.Add(entry);
            }
        }

        /// <summary>Stores the shared wallet snapshot captured from the active avatar.</summary>
        public void CaptureSharedWallet(WalletSaveData wallet)
        {
            SharedWallet = wallet;
            HasSharedPlayerState = true;
        }

        /// <summary>Stores the shared wallet snapshot loaded from a save file.</summary>
        public void LoadSharedWallet(WalletSaveData wallet)
        {
            SharedWallet = wallet;
            HasSharedPlayerState = true;
        }

        private bool AddAbility(WorldLayer world, string abilityId, bool notify)
        {
            string normalizedId = NormalizeAbilityId(abilityId);
            if (normalizedId.Length == 0) return false;

            if (!unlockedAbilities.TryGetValue(world, out HashSet<string> abilities))
            {
                abilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                unlockedAbilities[world] = abilities;
            }

            if (!abilities.Add(normalizedId)) return false;
            if (notify) AbilityUnlocked?.Invoke(world, normalizedId);
            return true;
        }

        private static string NormalizeAbilityId(string abilityId)
        {
            return string.IsNullOrWhiteSpace(abilityId)
                ? string.Empty
                : abilityId.Trim().ToLowerInvariant();
        }
    }
}
