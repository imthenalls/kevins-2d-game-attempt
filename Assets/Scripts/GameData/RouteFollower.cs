using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// A planar waypoint with no engine types. X/Y are the gameplay plane: world X/Z in the 3D Town,
    /// world X/Y in the 2D scenes. The Unity adapters convert their positions at the boundary.
    /// </summary>
    public struct PathPoint
    {
        public float X;
        public float Y;

        public PathPoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Engine-free route follower shared by every travelling NPC adapter (2D and 3D). It holds an
    /// ordered list of planar waypoints and the index of the one currently walked toward; advancing
    /// consumes every waypoint already within the arrival radius. Which waypoint is next and when the
    /// route is finished are navigation decisions, so they live in Core instead of being re-implemented
    /// in each MonoBehaviour. Wandering, commuting, chasing and door approach all drive this one type.
    ///
    /// Unity setup: none. Constructed by the NPC movement facades, which convert their engine positions
    /// into <see cref="PathPoint"/> values at the boundary.
    ///
    /// Runtime API:
    ///   SetRoute(List&lt;PathPoint&gt;) / Clear()
    ///   HasRoute, IsComplete, Remaining
    ///   TryCurrent(out float x, out float y)
    ///   Advance(currentX, currentY, reachedRadius) returns the number of waypoints consumed
    /// </summary>
    public sealed class RouteFollower
    {
        private List<PathPoint> route;
        private int index;

        /// <summary>True while a non-empty route is loaded (even after it is fully walked).</summary>
        public bool HasRoute => route != null && route.Count > 0;

        /// <summary>True when there is no route or every waypoint has been consumed.</summary>
        public bool IsComplete => !HasRoute || index >= route.Count;

        /// <summary>Number of waypoints not yet consumed.</summary>
        public int Remaining => HasRoute ? route.Count - index : 0;

        /// <summary>Loads a route and restarts at its first waypoint.</summary>
        public void SetRoute(List<PathPoint> points)
        {
            route = points;
            index = 0;
        }

        /// <summary>Unloads the current route.</summary>
        public void Clear()
        {
            route = null;
            index = 0;
        }

        /// <summary>Current waypoint, or false when the route is empty or exhausted.</summary>
        public bool TryCurrent(out float x, out float y)
        {
            if (IsComplete)
            {
                x = 0f;
                y = 0f;
                return false;
            }

            x = route[index].X;
            y = route[index].Y;
            return true;
        }

        /// <summary>
        /// Consumes every leading waypoint already within <paramref name="reachedRadius"/> of the
        /// current position and returns how many were consumed. Call it before reading the current
        /// waypoint each tick so a follower never hovers on a node it has already reached.
        /// </summary>
        public int Advance(float currentX, float currentY, float reachedRadius)
        {
            if (!HasRoute || reachedRadius < 0f)
                return 0;

            float radiusSqr = reachedRadius * reachedRadius;
            int consumed = 0;
            while (index < route.Count)
            {
                float dx = route[index].X - currentX;
                float dy = route[index].Y - currentY;
                if (dx * dx + dy * dy > radiusSqr)
                    break;

                index++;
                consumed++;
            }

            return consumed;
        }
    }
}
