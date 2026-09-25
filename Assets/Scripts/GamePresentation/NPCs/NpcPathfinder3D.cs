using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Thin Unity facade over the engine-free <see cref="GridPathfinder"/>. This component only samples
/// the world (which cells are blocked) by implementing <see cref="IWalkabilityGrid"/> with
/// <c>Physics.CheckBox</c>; all pathfinding logic lives in Game.Data and is unit-tested there.
///
/// Unity setup:
///   1. Add to an NPC GameObject alongside NpcSchedule3D.
///   2. Set Obstacle Layers to the Walls layer (buildings and perimeter walls).
///   3. Cell Size should match the layout grid (1 world unit by default).
///
/// Runtime API:
///   FindPath(start, goal) returns a List&lt;Vector3&gt; of waypoints (excluding the start), or null.
/// </summary>
[DisallowMultipleComponent]
public class NpcPathfinder3D : MonoBehaviour, IWalkabilityGrid
{
    [Tooltip("Layers that block movement (buildings, walls).")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    [Tooltip("World size of one logical cell. Should match the scene grid.")]
    [SerializeField, Min(0.01f)] private float cellSize = 1f;

    [Tooltip("How many cells beyond the start/goal bounding box to search.")]
    [SerializeField, Min(1)] private int searchPadding = 12;

    [SerializeField, Min(32)] private int maxNodes = 1500;

    [Tooltip("How many cells to search for a walkable start cell.")]
    [SerializeField, Min(0)] private int startSnapRadius = 4;

    /// <summary>Layers treated as obstacles by this pathfinder.</summary>
    public LayerMask ObstacleMask => obstacleLayers;

    // Per-search walkability cache: Physics.CheckBox per cell dominates path cost, and A* revisits
    // cells. Cleared at the start of every FindPath so dynamic obstacles are still respected.
    private readonly Dictionary<long, bool> walkableCache = new Dictionary<long, bool>();

    /// <summary>Finds a cell path between two world positions, or null when none exists.</summary>
    public List<Vector3> FindPath(Vector3 start, Vector3 goal)
    {
        walkableCache.Clear();

        int startX = Mathf.FloorToInt(start.x / cellSize);
        int startY = Mathf.FloorToInt(start.z / cellSize);
        int goalX = Mathf.FloorToInt(goal.x / cellSize);
        int goalY = Mathf.FloorToInt(goal.z / cellSize);

        List<(int x, int y)> cells =
            GridPathfinder.FindPath(this, startX, startY, goalX, goalY, searchPadding, maxNodes, startSnapRadius);

        if (cells == null)
            return null;

        var path = new List<Vector3>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
            path.Add(CellCenter(cells[i].x, cells[i].y, start.y));

        return path;
    }

    // ── IWalkabilityGrid (Unity sampling) ──────────────────────────────────────

    public bool IsWalkable(int cellX, int cellY)
    {
        long key = ((long)cellX << 32) ^ (uint)cellY;
        if (walkableCache.TryGetValue(key, out bool cached))
            return cached;

        Vector3 center = CellCenter(cellX, cellY, 0f) + Vector3.up * 0.5f;
        Vector3 half = new Vector3(cellSize * 0.45f, 0.5f, cellSize * 0.45f);
        bool walkable = !Physics.CheckBox(center, half, Quaternion.identity, obstacleLayers, QueryTriggerInteraction.Ignore);
        walkableCache[key] = walkable;
        return walkable;
    }

    public bool TryFindWalkable(int cellX, int cellY, int maxRadius, out int walkableCellX, out int walkableCellY)
    {
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != radius)
                        continue;

                    int cx = cellX + dx;
                    int cy = cellY + dz;
                    if (IsWalkable(cx, cy))
                    {
                        walkableCellX = cx;
                        walkableCellY = cy;
                        return true;
                    }
                }
            }
        }

        walkableCellX = cellX;
        walkableCellY = cellY;
        return false;
    }

    private Vector3 CellCenter(int cellX, int cellY, float y) =>
        new Vector3((cellX + 0.5f) * cellSize, y, (cellY + 0.5f) * cellSize);
}
