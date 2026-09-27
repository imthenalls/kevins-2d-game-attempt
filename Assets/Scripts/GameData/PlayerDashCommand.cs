namespace Game.Core
{
    /// <summary>
    /// Command returned by <see cref="PlayerDashModel"/> describing what the Unity adapter should do
    /// with the player's dash this tick. The adapter owns input, Rigidbody motion, and the trail; the
    /// model owns charges, cooldowns, recharge, and dash timing.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum PlayerDashCommand
    {
        /// <summary>Nothing to do.</summary>
        None,

        /// <summary>A dash started this tick; begin the trail and dash motion.</summary>
        StartDash,

        /// <summary>The active dash continues; keep applying dash motion.</summary>
        ContinueDash,

        /// <summary>The active dash ended this tick; stop dash motion and trail emission.</summary>
        StopDash,
    }
}
