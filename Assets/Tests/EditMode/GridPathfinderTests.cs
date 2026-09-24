using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the Core A* pathfinder. The walkability grid is a fake, so no scene or
    /// UnityEngine is needed — proof the algorithm lives in the right layer.
    /// </summary>
    public class GridPathfinderTests
    {
        private sealed class FakeGrid : IWalkabilityGrid
        {
            private readonly HashSet<(int x, int y)> blocked = new();

            public FakeGrid(params (int x, int y)[] blockedCells)
            {
                foreach (var cell in blockedCells)
                    blocked.Add(cell);
            }

            public bool IsWalkable(int cellX, int cellY) => !blocked.Contains((cellX, cellY));

            public bool TryFindWalkable(int cellX, int cellY, int maxRadius, out int wx, out int wy)
            {
                for (int radius = 0; radius <= maxRadius; radius++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != radius)
                                continue;

                            if (IsWalkable(cellX + dx, cellY + dy))
                            {
                                wx = cellX + dx;
                                wy = cellY + dy;
                                return true;
                            }
                        }
                    }
                }

                wx = cellX;
                wy = cellY;
                return false;
            }
        }

        [Test]
        public void Straight_Path_Excludes_Start_And_Reaches_Goal()
        {
            var grid = new FakeGrid();
            List<(int x, int y)> path = GridPathfinder.FindPath(grid, 0, 0, 3, 0, 8, 500, 2);

            Assert.IsNotNull(path);
            Assert.AreEqual(3, path.Count);
            Assert.AreEqual((3, 0), path[path.Count - 1]);
            Assert.IsFalse(path.Contains((0, 0)));
        }

        [Test]
        public void Blocked_Wall_Forces_A_Detour()
        {
            // Vertical wall at x = 1 except the gap at y = 2.
            var grid = new FakeGrid((1, 0), (1, 1), (1, 3), (1, 4));
            List<(int x, int y)> path = GridPathfinder.FindPath(grid, 0, 0, 2, 0, 8, 500, 2);

            Assert.IsNotNull(path);
            Assert.AreEqual((2, 0), path[path.Count - 1]);
            Assert.IsFalse(path.Contains((1, 0)), "path must not cross the wall");
            foreach (var cell in path)
                Assert.IsTrue(grid.IsWalkable(cell.x, cell.y), "every step must be walkable");
        }

        [Test]
        public void Start_Equals_Goal_Returns_Goal_Cell()
        {
            var grid = new FakeGrid();
            List<(int x, int y)> path = GridPathfinder.FindPath(grid, 4, 4, 4, 4, 8, 500, 0);

            Assert.IsNotNull(path);
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual((4, 4), path[0]);
        }

        [Test]
        public void Blocked_Start_Snaps_To_A_Walkable_Cell()
        {
            var grid = new FakeGrid((0, 0));
            List<(int x, int y)> path = GridPathfinder.FindPath(grid, 0, 0, 2, 0, 8, 500, 2);

            Assert.IsNotNull(path);
            Assert.AreEqual((2, 0), path[path.Count - 1]);
        }

        [Test]
        public void Fully_Walled_In_Goal_Is_Unreachable()
        {
            var grid = new FakeGrid((1, 0), (0, 1), (-1, 0), (0, -1));
            List<(int x, int y)> path = GridPathfinder.FindPath(grid, 0, 0, 3, 3, 6, 500, 2);

            Assert.IsNull(path);
        }
    }
}
