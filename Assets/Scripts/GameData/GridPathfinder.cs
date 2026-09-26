using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free 4-directional A* over an <see cref="IWalkabilityGrid"/>. The grid's collider
    /// sampling stays in the Unity adapter; the algorithm, bounds, snap and reconstruction live here
    /// so they are identical for every dimension and unit-testable without a scene.
    ///
    /// Unity setup: none — static class.
    ///
    /// Runtime API: FindPath(grid, startX, startY, goalX, goalY, padding, maxNodes, snapRadius,
    /// requireWalkableGoal) returns the cells to walk through (excluding the start cell), or null
    /// when unreachable.
    ///
    /// When <c>requireWalkableGoal</c> is false (default) the goal cell may be blocked — the legacy
    /// behavior older 2D door interactions rely on (pathing up to a closed gate). Set it true to
    /// demand a genuinely standable destination.
    /// </summary>
    public static class GridPathfinder
    {
        private static readonly (int dx, int dy)[] Directions =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        public static List<(int x, int y)> FindPath(
            IWalkabilityGrid grid,
            int startX, int startY,
            int goalX, int goalY,
            int searchPadding, int maxNodes, int startSnapRadius,
            bool requireWalkableGoal = false)
        {
            if (grid == null)
                return null;

            if (!grid.TryFindWalkable(startX, startY, startSnapRadius, out int sx, out int sy))
                return null;

            (int x, int y) start = (sx, sy);
            (int x, int y) goal = (goalX, goalY);

            if (start == goal)
                return new List<(int x, int y)> { goal };

            if (requireWalkableGoal && !grid.IsWalkable(goal.x, goal.y))
                return null;

            int minX = System.Math.Min(start.x, goal.x) - searchPadding;
            int maxX = System.Math.Max(start.x, goal.x) + searchPadding;
            int minY = System.Math.Min(start.y, goal.y) - searchPadding;
            int maxY = System.Math.Max(start.y, goal.y) + searchPadding;

            var open = new List<(int x, int y)> { start };
            var cameFrom = new Dictionary<(int x, int y), (int x, int y)>();
            var gScore = new Dictionary<(int x, int y), float> { [start] = 0f };
            var fScore = new Dictionary<(int x, int y), float> { [start] = Heuristic(start, goal) };
            var closed = new HashSet<(int x, int y)>();
            int expanded = 0;

            while (open.Count > 0 && expanded < maxNodes)
            {
                int bestIndex = 0;
                float bestF = fScore[open[0]];
                for (int i = 1; i < open.Count; i++)
                {
                    float f = fScore[open[i]];
                    if (f < bestF) { bestF = f; bestIndex = i; }
                }

                (int x, int y) current = open[bestIndex];
                if (current == goal)
                    return Reconstruct(cameFrom, current);

                open.RemoveAt(bestIndex);
                closed.Add(current);
                expanded++;

                for (int d = 0; d < Directions.Length; d++)
                {
                    (int x, int y) next = (current.x + Directions[d].dx, current.y + Directions[d].dy);
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY)
                        continue;
                    if (closed.Contains(next))
                        continue;
                    // The goal may be blocked (e.g. a gate cell); every other step must be walkable.
                    if (next != goal && !grid.IsWalkable(next.x, next.y))
                        continue;

                    float tentative = gScore[current] + 1f;
                    if (gScore.TryGetValue(next, out float existing) && tentative >= existing)
                        continue;

                    cameFrom[next] = current;
                    gScore[next] = tentative;
                    fScore[next] = tentative + Heuristic(next, goal);
                    if (!open.Contains(next))
                        open.Add(next);
                }
            }

            return null;
        }

        private static List<(int x, int y)> Reconstruct(
            Dictionary<(int x, int y), (int x, int y)> cameFrom, (int x, int y) current)
        {
            var cells = new List<(int x, int y)> { current };
            while (cameFrom.TryGetValue(current, out (int x, int y) previous))
            {
                current = previous;
                cells.Add(current);
            }

            cells.Reverse();
            if (cells.Count > 1)
                cells.RemoveAt(0); // drop the start cell so the follower does not walk backward first
            return cells;
        }

        private static float Heuristic((int x, int y) a, (int x, int y) b) =>
            System.Math.Abs(a.x - b.x) + System.Math.Abs(a.y - b.y);
    }
}
