namespace Game.Core
{
    /// <summary>What a chase adapter should do on one navigation tick.</summary>
    public enum NpcChaseNavigationAction
    {
        /// <summary>No movement this tick.</summary>
        None,

        /// <summary>Ignore the path and step straight at the target (clear line, or beyond path range).</summary>
        DriveDirect,

        /// <summary>Walk the currently committed route's next waypoint.</summary>
        FollowRoute,

        /// <summary>Recompute the path to the target now, then report the result back to the policy.</summary>
        Repath,

        /// <summary>Hold position because the target is blocked and no usable route is available.</summary>
        Wait,
    }
}
