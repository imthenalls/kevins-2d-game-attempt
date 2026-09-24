namespace Game.Core
{
    /// <summary>
    /// Engine-free view of a cell grid's walkability, consumed by <see cref="GridPathfinder"/>. The
    /// Unity adapter implements this (sampling colliders), so the pathfinding algorithm itself stays
    /// in Game.Data and is testable without a scene.
    ///
    /// Unity setup: none — implemented by a MonoBehaviour (e.g. NpcPathfinder3D).
    /// </summary>
    public interface IWalkabilityGrid
    {
        /// <summary>True when the cell is free to stand on.</summary>
        bool IsWalkable(int cellX, int cellY);

        /// <summary>Finds the nearest walkable cell within <paramref name="maxRadius"/> rings.</summary>
        bool TryFindWalkable(int cellX, int cellY, int maxRadius, out int walkableCellX, out int walkableCellY);
    }
}
