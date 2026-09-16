namespace Game.Core
{
    /// <summary>
    /// Which grid axis a gate's cells progress along. The halves retract along this axis.
    /// Plain enum (no UnityEngine), lives in Game.Data so SlidingDoorConfig can reference it.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum DoorAxis
    {
        /// <summary>Cells advance along the grid +X axis (down-right diamond edge).</summary>
        GridX,
        /// <summary>Cells advance along the grid +Y axis (up-right diamond edge).</summary>
        GridY
    }
}
