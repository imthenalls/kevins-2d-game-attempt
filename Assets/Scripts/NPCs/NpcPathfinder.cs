using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grid A* pathfinding for NPCs. Works directly on the isometric Grid cells and treats any
/// solid (non-trigger) collider as an obstacle, so walls, closed gates, and props are avoided
/// automatically. Returns world-space waypoints for a behavior to follow.
///
/// Unity setup:
///   1. Add to an NPC GameObject that has a Rigidbody2D and NpcBehaviorManager.
///   2. Set Obstacle Layers to the layers that block movement (walls, props, closed gates).
///   3. The grid is found automatically (nearest Grid); call SetGrid to override.
///
/// Runtime API:
///   FindPath(start, goal) returns a List&lt;Vector2&gt; of waypoints (without the start cell),
///   or null when no path is found. Call from a behavior's Enter and follow in Tick.
/// </summary>
[DisallowMultipleComponent]
public class NpcPathfinder : MonoBehaviour
{
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [Tooltip("How many cells beyond the start/goal bounding box to search.")]
    [SerializeField, Min(1)] private int searchPadding = 10;
    [SerializeField, Min(32)] private int maxNodes = 1200;
    [Tooltip("How many cells to search for a walkable start cell.")]
    [SerializeField, Min(0)] private int startSnapRadius = 3;

    private Grid grid;
    private Collider2D[] ownColliders;
    private readonly Collider2D[] overlap = new Collider2D[16];

    private static readonly Vector3Int[] Directions =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    private void Awake() => ownColliders = GetComponentsInChildren<Collider2D>();

    /// <summary>Overrides the auto-detected grid.</summary>
    public void SetGrid(Grid value) => grid = value;

    /// <summary>True when the world position's cell is walkable. Useful for diagnostics.</summary>
    public bool IsWalkableWorld(Vector2 world)
    {
        EnsureGrid();
        return grid != null && IsWalkable(grid.WorldToCell(world));
    }

    /// <summary>The grid cell for a world position (diagnostics).</summary>
    public Vector3Int WorldToCell(Vector2 world)
    {
        EnsureGrid();
        return grid != null ? grid.WorldToCell(world) : Vector3Int.zero;
    }

    /// <summary>Finds the nearest walkable cell to a world position (diagnostics).</summary>
    public bool TryGetWalkableCell(Vector2 world, int radius, out Vector3Int cell)
    {
        EnsureGrid();
        if (grid == null)
        {
            cell = Vector3Int.zero;
            return false;
        }

        return TryFindWalkable(grid.WorldToCell(world), radius, out cell);
    }

    /// <summary>Finds a cell path between two world positions, or null when none exists.</summary>
    public List<Vector2> FindPath(Vector2 start, Vector2 goal)
    {
        EnsureGrid();
        if (grid == null)
            return null;

        // Snap start to the nearest walkable cell so a body clipping a wall still gets a path.
        // The goal is used as-is so the agent paths to the gate cell from its own side; the goal
        // cell is allowed to be blocked (it is the gate).
        if (!TryFindWalkable(grid.WorldToCell(start), startSnapRadius, out Vector3Int startCell))
            return null;
        Vector3Int goalCell = grid.WorldToCell(goal);

        if (startCell == goalCell)
            return new List<Vector2> { grid.GetCellCenterWorld(goalCell) };

        int minX = Mathf.Min(startCell.x, goalCell.x) - searchPadding;
        int maxX = Mathf.Max(startCell.x, goalCell.x) + searchPadding;
        int minY = Mathf.Min(startCell.y, goalCell.y) - searchPadding;
        int maxY = Mathf.Max(startCell.y, goalCell.y) + searchPadding;

        var open = new List<Vector3Int> { startCell };
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float> { [startCell] = 0f };
        var fScore = new Dictionary<Vector3Int, float> { [startCell] = Heuristic(startCell, goalCell) };
        var closed = new HashSet<Vector3Int>();
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

            Vector3Int current = open[bestIndex];
            if (current == goalCell)
                return Reconstruct(cameFrom, current);

            open.RemoveAt(bestIndex);
            closed.Add(current);
            expanded++;

            for (int d = 0; d < Directions.Length; d++)
            {
                Vector3Int next = current + Directions[d];
                if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY)
                    continue;
                if (closed.Contains(next))
                    continue;
                if (next != goalCell && !IsWalkable(next))
                    continue;

                float tentative = gScore[current] + 1f;
                if (gScore.TryGetValue(next, out float existing) && tentative >= existing)
                    continue;

                cameFrom[next] = current;
                gScore[next] = tentative;
                fScore[next] = tentative + Heuristic(next, goalCell);
                if (!open.Contains(next))
                    open.Add(next);
            }
        }

        return null;
    }

    // Searches outward in square rings for the closest walkable cell.
    private bool TryFindWalkable(Vector3Int center, int maxRadius, out Vector3Int cell)
    {
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius)
                        continue;

                    Vector3Int candidate = center + new Vector3Int(dx, dy, 0);
                    if (IsWalkable(candidate))
                    {
                        cell = candidate;
                        return true;
                    }
                }
            }
        }

        cell = center;
        return false;
    }

    private void EnsureGrid()
    {
        if (grid != null)
            return;

        Grid nearest = null;
        float nearestSqr = float.MaxValue;
        Vector2 origin = transform.position;
        foreach (Grid candidate in Object.FindObjectsByType<Grid>(FindObjectsSortMode.None))
        {
            float sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        grid = nearest;
    }

    private List<Vector2> Reconstruct(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
    {
        var cells = new List<Vector3Int> { current };
        while (cameFrom.TryGetValue(current, out Vector3Int previous))
        {
            current = previous;
            cells.Add(current);
        }

        cells.Reverse();
        var path = new List<Vector2>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
            path.Add(grid.GetCellCenterWorld(cells[i]));

        // Drop the start cell so the follower doesn't walk backward first.
        if (path.Count > 1)
            path.RemoveAt(0);
        return path;
    }

    private bool IsWalkable(Vector3Int cell)
    {
        Vector3 center = grid.GetCellCenterWorld(cell);
        int count = Physics2D.OverlapPointNonAlloc(center, overlap, obstacleLayers);
        for (int i = 0; i < count; i++)
        {
            Collider2D contact = overlap[i];
            if (contact == null || contact.isTrigger || IsOwn(contact))
                continue;
            return false;
        }

        return true;
    }

    private bool IsOwn(Collider2D candidate)
    {
        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == candidate)
                return true;
        }

        return false;
    }

    private static float Heuristic(Vector3Int a, Vector3Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
}
