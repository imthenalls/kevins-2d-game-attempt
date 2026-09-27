namespace Game.Core
{
    /// <summary>
    /// Unity operation requested by <see cref="NpcUseDoorModel"/>. The adapter executes movement,
    /// interaction, and completion; the model owns the phase transitions and eligibility rules.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum NpcDoorCommand
    {
        /// <summary>Stop and re-evaluate next tick (for example, the route is exhausted short of the gate).</summary>
        None = 0,

        /// <summary>Stop moving and wait (gate is mid-animation, or busy).</summary>
        Wait = 1,

        /// <summary>Advance the current route and move toward its waypoint (or the direct target).</summary>
        FollowRoute = 2,

        /// <summary>Call gate.TryUse and feed the result back with ResolveUse.</summary>
        AttemptUse = 3,

        /// <summary>Switch to the pass-through phase and walk through the opening.</summary>
        BeginPass = 4,

        /// <summary>The behavior is finished; stop moving.</summary>
        Complete = 5,
    }
}
