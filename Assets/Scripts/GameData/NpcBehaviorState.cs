namespace Game.Core
{
    /// <summary>
    /// Behavioral state of an NPC. Owned by the Core so gameplay rules (interaction gating, AI
    /// pausing, combat) can be expressed engine-free and shared by 2D and 3D adapters.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum NpcBehaviorState
    {
        Idle,
        Talking,
        Disabled,
        Combat
    }
}
