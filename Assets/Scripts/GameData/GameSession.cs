namespace Game.Core
{
    /// <summary>
    /// Scoped root that owns authoritative, saveable gameplay models for a play session. A Unity
    /// composition root (GameSessionHost) creates and exposes one instance; tests create their own
    /// without any Unity types.
    ///
    /// Unity setup: none — plain C#. See GameSessionHost for the Unity bootstrap.
    ///
    /// Runtime API: Npcs (repository), NpcStates (command service), the NPC schedule
    /// repository/service (NpcScheduleRepo / NpcSchedules), the NPC knowledge
    /// repository/service (NpcMemoryRepo / NpcMemories), and the starting-inventory initialization
    /// service (NpcInventories).
    /// </summary>
    public sealed class GameSession
    {
        public NpcStateRepository Npcs { get; }
        public NpcStateService NpcStates { get; }
        public NpcScheduleRepository NpcScheduleRepo { get; }
        public NpcScheduleService NpcSchedules { get; }
        public NpcMemoryRepository NpcMemoryRepo { get; }
        public NpcMemoryService NpcMemories { get; }
        public NpcInventoryInitializationService NpcInventories { get; }

        /// <summary>
        /// Authoritative world-travel state: active world, per-world remembered positions,
        /// world-scoped ability unlocks, and the shared player wallet snapshot. Lives for the whole
        /// session so it survives scene loads and avatar changes.
        /// </summary>
        public WorldTravelModel WorldTravel { get; }

        /// <summary>
        /// Authoritative ledger of quest rewards that could not be delivered (full inventory) and
        /// remain claimable. Lives for the whole session and is saved via SaveData.pendingRewards.
        /// </summary>
        public PendingRewardLedger PendingRewards { get; }

        /// <summary>
        /// Per-character cumulative gameplay statistics (attacks, damage, kills, crits, items,
        /// money), keyed by a stable character id so they outlive the component.
        /// </summary>
        public CharacterStatisticsRepository Statistics { get; }

        /// <summary>
        /// Authoritative player health, shared across avatars and scene loads. Null until the first
        /// player binds; call <see cref="GetOrCreatePlayerHealth"/> to seed and retrieve it.
        /// </summary>
        public HealthModel PlayerHealth { get; private set; }

        /// <summary>
        /// Authoritative player position (logical cell + local offset), shared across avatars and
        /// scene loads. Null until the first player binds; call
        /// <see cref="GetOrCreatePlayerPosition"/> to seed and retrieve it.
        /// </summary>
        public PositionModel PlayerPosition { get; private set; }

        public GameSession()
        {
            Npcs = new NpcStateRepository();
            NpcStates = new NpcStateService(Npcs);
            NpcScheduleRepo = new NpcScheduleRepository();
            NpcSchedules = new NpcScheduleService(NpcScheduleRepo);
            NpcMemoryRepo = new NpcMemoryRepository();
            NpcMemories = new NpcMemoryService(NpcMemoryRepo);
            NpcInventories = new NpcInventoryInitializationService();
            WorldTravel = new WorldTravelModel();
            PendingRewards = new PendingRewardLedger();
            Statistics = new CharacterStatisticsRepository();
        }

        /// <summary>
        /// Returns the player's health model, creating it from the given seed values on first use.
        /// Later callers adopt the existing model so player HP persists across avatar/scene changes.
        /// </summary>
        public HealthModel GetOrCreatePlayerHealth(int maxHp, int hp)
        {
            if (PlayerHealth == null)
                PlayerHealth = new HealthModel(maxHp, hp);
            return PlayerHealth;
        }

        /// <summary>
        /// Returns the player's position model, creating it from the given seed values on first use.
        /// Later callers adopt the existing model so position persists across avatar/scene changes.
        /// </summary>
        public PositionModel GetOrCreatePlayerPosition(int cellX, int cellY, float offsetX, float offsetY)
        {
            if (PlayerPosition == null)
                PlayerPosition = new PositionModel(cellX, cellY, offsetX, offsetY);
            return PlayerPosition;
        }
    }
}
