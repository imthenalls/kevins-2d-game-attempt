using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free remembered return position for one world: the scene it belongs to plus a
    /// grid-anchored position (logical cell + local offset). Legacy float-only positions are retained
    /// as a fallback for older saves and for scenes with no Grid; the Unity adapter converts them.
    ///
    /// Owned by <see cref="WorldTravelModel"/>. Unity setup: none.
    /// </summary>
    public sealed class RememberedWorldPosition
    {
        public string Scene { get; set; }

        /// <summary>True when CellX/CellY/OffsetX/OffsetY are valid (grid-anchored form).</summary>
        public bool HasCell { get; set; }
        public int CellX { get; set; }
        public int CellY { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }

        // Legacy world floats, kept for older saves and as a no-Grid fallback.
        public float LegacyX { get; set; }
        public float LegacyY { get; set; }
        public float LegacyZ { get; set; }

        public static RememberedWorldPosition FromSave(WorldPositionSaveEntry entry)
        {
            if (entry == null) return null;

            return new RememberedWorldPosition
            {
                Scene = entry.scene,
                HasCell = entry.hasCell,
                CellX = entry.cellX,
                CellY = entry.cellY,
                OffsetX = entry.offsetX,
                OffsetY = entry.offsetY,
                LegacyX = entry.x,
                LegacyY = entry.y,
                LegacyZ = entry.z,
            };
        }

        public void WriteTo(WorldPositionSaveEntry entry)
        {
            if (entry == null) return;

            entry.scene = Scene;
            entry.hasCell = HasCell;
            entry.cellX = CellX;
            entry.cellY = CellY;
            entry.offsetX = OffsetX;
            entry.offsetY = OffsetY;
            entry.x = LegacyX;
            entry.y = LegacyY;
            entry.z = LegacyZ;
        }
    }
}
