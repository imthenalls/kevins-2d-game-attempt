using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free authoritative position: a logical grid cell plus a local offset from the cell
    /// centre. Grid-anchored (deterministic against scene content, testable without a scene) yet
    /// able to restore the exact position within a cell, so loading does not snap.
    ///
    /// Owned by <see cref="GameSession"/> for the player, so it survives avatar destroy/recreate and
    /// can be relocated by gameplay. A Unity adapter mirrors the physics transform to/from it.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class PositionModel
    {
        public int CellX { get; private set; }
        public int CellY { get; private set; }

        /// <summary>Horizontal offset from the cell centre, in world units.</summary>
        public float OffsetX { get; private set; }

        /// <summary>Vertical offset from the cell centre, in world units.</summary>
        public float OffsetY { get; private set; }

        /// <summary>Raised whenever the stored position changes.</summary>
        public event Action<PositionModel> Changed;

        public PositionModel(int cellX, int cellY, float offsetX = 0f, float offsetY = 0f)
        {
            CellX = cellX;
            CellY = cellY;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        /// <summary>Moves the model; raises <see cref="Changed"/> only when the value differs.</summary>
        public void Set(int cellX, int cellY, float offsetX, float offsetY)
        {
            if (CellX == cellX && CellY == cellY &&
                OffsetX == offsetX && OffsetY == offsetY)
                return;

            CellX = cellX;
            CellY = cellY;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Changed?.Invoke(this);
        }
    }
}
