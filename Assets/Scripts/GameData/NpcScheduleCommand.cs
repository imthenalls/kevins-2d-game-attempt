using System;

namespace Game.Core
{
    /// <summary>
    /// Unity operations the home schedule decides to perform. Returned (possibly combined) by
    /// <see cref="NpcScheduleState"/> so the MonoBehaviour only executes them and reports the outcome
    /// back. No Unity dependency.
    ///
    /// Unity setup: none.
    /// </summary>
    [Flags]
    public enum NpcScheduleCommand
    {
        None = 0,

        /// <summary>Build a route to the home entrance (report RouteFailed if none exists).</summary>
        RequestRoute = 1 << 0,

        /// <summary>Enter the home through the town door portal (report PortalSucceeded/Failed).</summary>
        EnterHomePortal = 1 << 1,

        /// <summary>Leave the home through the interior portal (report PortalSucceeded/Failed).</summary>
        LeaveHomePortal = 1 << 2,

        /// <summary>Open (unlock) the owner's home door.</summary>
        OpenHomeDoor = 1 << 3,

        /// <summary>Close (relock) the owner's home door.</summary>
        CloseHomeDoor = 1 << 4,
    }
}
