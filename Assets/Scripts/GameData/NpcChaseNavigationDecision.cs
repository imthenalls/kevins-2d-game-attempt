namespace Game.Core
{
    /// <summary>
    /// What the chase adapter should do this tick, returned by
    /// <see cref="NpcChaseNavigationPolicy.Evaluate"/> and
    /// <see cref="NpcChaseNavigationPolicy.ReportPathResult"/>.
    ///
    /// Unity setup: none.
    /// </summary>
    public struct NpcChaseNavigationDecision
    {
        /// <summary>The action to perform this tick.</summary>
        public NpcChaseNavigationAction Action;

        /// <summary>Ignore the path and step straight at the target (clear line, or beyond path range).</summary>
        public bool DriveDirect => Action == NpcChaseNavigationAction.DriveDirect;

        /// <summary>Recompute the path to the target now, then follow it.</summary>
        public bool Repath => Action == NpcChaseNavigationAction.Repath;

        /// <summary>Walk the currently committed route's next waypoint.</summary>
        public bool FollowRoute => Action == NpcChaseNavigationAction.FollowRoute;

        /// <summary>Hold position because the target is blocked and no usable route is available.</summary>
        public bool Wait => Action == NpcChaseNavigationAction.Wait;
    }
}
