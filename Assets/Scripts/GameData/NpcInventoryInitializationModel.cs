namespace Game.Core
{
    /// <summary>
    /// Engine-free record of whether one NPC has received its one-time starting inventory, or had an
    /// authoritative inventory restored from a save. Keyed by stable npcId so it outlives the scene
    /// view and can be tested without a scene.
    ///
    /// Unity setup: none. Owned by GameSession through NpcInventoryInitializationService.
    ///
    /// Runtime API: Initialized, MarkInitialized, Reset.
    /// </summary>
    public sealed class NpcInventoryInitializationModel
    {
        /// <summary>True once this NPC's starting inventory has been applied or restored.</summary>
        public bool Initialized { get; private set; }

        public void MarkInitialized() => Initialized = true;

        public void Reset() => Initialized = false;
    }
}
