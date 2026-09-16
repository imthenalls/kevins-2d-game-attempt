namespace Game.Core
{
    /// <summary>
    /// Outcome of an entity attempting to use a gate. Returned by SlidingDoor.TryUse so scripts
    /// and AI can react (for example, an NPC remembering that a gate is locked). Plain enum, lives
    /// in Game.Data.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum GateUseResult
    {
        /// <summary>The gate is open (opened now, or was already open).</summary>
        Opened,
        /// <summary>The required key was missing, so the gate stayed closed.</summary>
        Locked,
        /// <summary>The gate is mid-animation; try again shortly.</summary>
        Busy,
        /// <summary>The gate cannot be used (disabled or not built).</summary>
        Unavailable
    }
}
