namespace Game.Core
{
    /// <summary>
    /// Explicit phases of the NPC use-door behavior. Approach walks to the gate and tries to open
    /// it; PassThrough walks through the opening to the far side.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum NpcDoorPhase
    {
        Approach = 0,
        PassThrough = 1,
    }
}
