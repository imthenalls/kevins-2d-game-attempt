namespace Game.Core
{
    /// <summary>
    /// Inputs the schedule facade reports to <see cref="NpcScheduleState"/>. The facade performs the
    /// Unity side (pathfinding, portal travel) and reports the outcome here; the state machine decides
    /// what happens next.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum NpcScheduleEvent
    {
        /// <summary>The route to the home entrance could not be built (or was abandoned mid-trip).</summary>
        RouteFailed = 0,

        /// <summary>The NPC reached the home door approach (or finished its route).</summary>
        ReachedEntrance = 1,

        /// <summary>The portal travel the model requested completed.</summary>
        PortalSucceeded = 2,

        /// <summary>The portal travel the model requested failed.</summary>
        PortalFailed = 3,
    }
}
