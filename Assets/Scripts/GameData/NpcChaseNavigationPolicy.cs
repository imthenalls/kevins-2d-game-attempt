using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free chase navigation policy: decides when the enemy may step straight at its target,
    /// when it must recompute a path (cadence, or the target moved far enough), when to follow the
    /// committed route, and when to hold because no usable route exists. Line-of-sight raycasts,
    /// pathfinding, and motion stay in the Unity adapter, which reports <c>hasClearLine</c> and then
    /// feeds the pathfinder result back through <see cref="ReportPathResult"/>.
    ///
    /// Held by NpcChaseNavigator. Unity setup: none.
    /// </summary>
    public sealed class NpcChaseNavigationPolicy
    {
        private enum PathState
        {
            /// <summary>No route has been produced yet this approach.</summary>
            None,

            /// <summary>A usable route exists and may be followed.</summary>
            Ready,

            /// <summary>The last repath failed, or the route was exhausted, while the target is blocked.</summary>
            Blocked,
        }

        private readonly NpcChaseNavigationConfig config;

        private float nextRepathAt;
        private float lastGoalX;
        private float lastGoalZ;
        private bool hasGoal;
        private PathState pathState = PathState.None;

        public NpcChaseNavigationPolicy(NpcChaseNavigationConfig config)
            => this.config = config ?? new NpcChaseNavigationConfig();

        /// <summary>
        /// Evaluates one chase tick. <paramref name="hasClearLine"/> is the adapter's line-of-sight
        /// result; <paramref name="repathInterval"/> is the caller's requested recompute cadence in
        /// seconds (clamped to the configured minimum). The adapter still owns path generation: it
        /// acts on <see cref="NpcChaseNavigationAction.Repath"/> and then calls
        /// <see cref="ReportPathResult"/>.
        /// </summary>
        public NpcChaseNavigationDecision Evaluate(
            float now,
            float selfX,
            float selfZ,
            float targetX,
            float targetZ,
            bool hasClearLine,
            float repathInterval)
        {
            float dx = targetX - selfX;
            float dz = targetZ - selfZ;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);

            // A clear line or an out-of-range target means step straight; forget the path so the next
            // blocked tick recomputes a fresh one instead of leaning on a stale route.
            if (hasClearLine || distance > config.MaxPathDistance)
            {
                hasGoal = false;
                pathState = PathState.None;
                return new NpcChaseNavigationDecision { Action = NpcChaseNavigationAction.DriveDirect };
            }

            float goalDx = targetX - lastGoalX;
            float goalDz = targetZ - lastGoalZ;
            bool targetMoved = !hasGoal ||
                goalDx * goalDx + goalDz * goalDz > config.RepathTargetMoved * config.RepathTargetMoved;
            bool timeElapsed = !hasGoal || now >= nextRepathAt;

            if (targetMoved || timeElapsed)
            {
                float interval = repathInterval < config.MinRepathInterval
                    ? config.MinRepathInterval
                    : repathInterval;
                nextRepathAt = now + interval;
                lastGoalX = targetX;
                lastGoalZ = targetZ;
                hasGoal = true;
                return new NpcChaseNavigationDecision { Action = NpcChaseNavigationAction.Repath };
            }

            // Not due for a repath: follow a ready route, otherwise hold rather than walking into the
            // obstacle the pathfinder just failed to route around.
            return new NpcChaseNavigationDecision
            {
                Action = pathState == PathState.Ready
                    ? NpcChaseNavigationAction.FollowRoute
                    : NpcChaseNavigationAction.Wait,
            };
        }

        /// <summary>
        /// The adapter reports whether the requested path was produced. Returns the follow-up action:
        /// follow the new route when one exists, otherwise wait until the next repath is due.
        /// </summary>
        public NpcChaseNavigationDecision ReportPathResult(bool foundPath)
        {
            if (foundPath)
            {
                pathState = PathState.Ready;
                return new NpcChaseNavigationDecision { Action = NpcChaseNavigationAction.FollowRoute };
            }

            pathState = PathState.Blocked;
            return new NpcChaseNavigationDecision { Action = NpcChaseNavigationAction.Wait };
        }

        /// <summary>
        /// The adapter reports that the committed route was fully consumed while the target is still
        /// blocked. The policy holds until the next repath is due instead of a direct fallback.
        /// </summary>
        public void ReportRouteExhausted() => pathState = PathState.Blocked;
    }
}
