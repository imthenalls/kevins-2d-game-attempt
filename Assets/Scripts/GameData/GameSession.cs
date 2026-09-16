namespace Game.Core
{
    /// <summary>
    /// Scoped root that owns authoritative, saveable gameplay models for a play session. A Unity
    /// composition root (GameSessionHost) creates and exposes one instance; tests create their own
    /// without any Unity types.
    ///
    /// Unity setup: none — plain C#. See GameSessionHost for the Unity bootstrap.
    ///
    /// Runtime API: Npcs (repository) and NpcStates (command service).
    /// </summary>
    public sealed class GameSession
    {
        public NpcStateRepository Npcs { get; }
        public NpcStateService NpcStates { get; }

        public GameSession()
        {
            Npcs = new NpcStateRepository();
            NpcStates = new NpcStateService(Npcs);
        }
    }
}
