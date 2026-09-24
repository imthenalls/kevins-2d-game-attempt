namespace Game.Core
{
    /// <summary>
    /// Scoped root that owns authoritative, saveable gameplay models for a play session. A Unity
    /// composition root (GameSessionHost) creates and exposes one instance; tests create their own
    /// without any Unity types.
    ///
    /// Unity setup: none — plain C#. See GameSessionHost for the Unity bootstrap.
    ///
    /// Runtime API: Npcs (repository), NpcStates (command service), and the NPC schedule
    /// repository/service (NpcScheduleRepo / NpcSchedules).
    /// </summary>
    public sealed class GameSession
    {
        public NpcStateRepository Npcs { get; }
        public NpcStateService NpcStates { get; }
        public NpcScheduleRepository NpcScheduleRepo { get; }
        public NpcScheduleService NpcSchedules { get; }

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
