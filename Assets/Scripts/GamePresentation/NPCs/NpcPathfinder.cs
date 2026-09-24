using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Thin Unity facade over the engine-free <see cref="GridPathfinder"/>. This component only samples
/// the world (which cells are blocked) by implementing <see cref="IWalkabilityGrid"/> with
/// <c>Physics2D.OverlapPoint</c>; the A* algorithm lives in Game.Data and is unit-tested there.
///
/// Unity setup:
///   1. Add to an NPC GameObject that has a Rigidbody2D and NpcBehaviorManager.
///   2. Set Obstacle Layers to the layers that block movement (walls, props, closed gates).
///   3. The grid is found automatically (nearest Grid); call SetGrid to override.
///
/// Runtime API:
///   FindPath(start, goal) returns a List&lt;Vector2&gt; of waypoints (without the start cell),
///   or null when no path is found.
/// </summary>
[DisallowMultipleComponent]
public class NpcPathfinder : MonoBehaviour, IWalkabilityGrid
{
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [Tooltip("How many cells beyond the start/goal bounding box to search.")]
    [SerializeField, Min(1)] private int searchPadding = 10;
    [SerializeField, Min(32)] private int maxNodes = 1200;
    [Tooltip("How many cells to search for a walkable start cell.")]
    [SerializeField, Min(0)] private int startSnapRadius = 3;

    private Grid grid;
    private Collider2D[] ownColliders;
    private readonly List<Collider2D> overlap = new List<Collider2D>();

    private void Awake() => ownColliders = GetComponentsInChildren<Collider2D>();

    /// <summary>Overrides the auto-detected grid.</summary>
    public void SetGrid(Grid value) => grid = value;

    /// <summary>True when the world position's cell is walkable. Useful for diagnostics.</summary>
    public bool IsWalkableWorld(Vector2 world)
    {
        EnsureGrid();
        if (grid == null)
            return false;

        Vector3Int cell = grid.WorldToCell(world);
        return IsWalkable(cell.x, cell.y);
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

        Vector3Int origin = grid.WorldToCell(world);
        if (TryFindWalkable(origin.x, origin.y, radius, out int wx, out int wy))
        {
            cell = new Vector3Int(wx, wy, 0);
            return true;
        }

        cell = origin;
        return false;
    }

    /// <summary>Finds a cell path between two world positions, or null when none exists.</summary>
    public List<Vector2> FindPath(Vector2 start, Vector2 goal)
    {
        EnsureGrid();
        if (grid == null)
            return null;

        Vector3Int startCell = grid.WorldToCell(start);
        Vector3Int goalCell = grid.WorldToCell(goal);

        List<(int x, int y)> cells = GridPathfinder.FindPath(
            this, startCell.x, startCell.y, goalCell.x, goalCell.y, searchPadding, maxNodes, startSnapRadius);

        if (cells == null)
            return null;

        var path = new List<Vector2>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
            path.Add(grid.GetCellCenterWorld(new Vector3Int(cells[i].x, cells[i].y, 0)));

        return path;
    }

    // ── IWalkabilityGrid (Unity sampling) ──────────────────────────────────────

    public bool IsWalkable(int cellX, int cellY)
    {
        EnsureGrid();
        if (grid == null)
            return false;

        Vector3 center = grid.GetCellCenterWorld(new Vector3Int(cellX, cellY, 0));
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = true };
        int count = Physics2D.OverlapPoint(center, filter, overlap);
        for (int i = 0; i < count; i++)
        {
            Collider2D contact = overlap[i];
            if (contact == null || contact.isTrigger || IsOwn(contact))
                continue;
            return false;
        }

        return true;
    }

    public bool TryFindWalkable(int cellX, int cellY, int maxRadius, out int walkableCellX, out int walkableCellY)
    {
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius)
                        continue;

                    if (IsWalkable(cellX + dx, cellY + dy))
                    {
                        walkableCellX = cellX + dx;
                        walkableCellY = cellY + dy;
                        return true;
                    }
                }
            }
        }

        walkableCellX = cellX;
        walkableCellY = cellY;
        return false;
    }

    private void EnsureGrid()
    {
        if (grid != null)
            return;

        Grid nearest = null;
        float nearestSqr = float.MaxValue;
        Vector2 origin = transform.position;
        foreach (Grid candidate in Object.FindObjectsByType<Grid>())
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

    private bool IsOwn(Collider2D candidate)
    {
        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == candidate)
                return true;
        }

        return false;
    }
}
