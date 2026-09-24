namespace Game.Core
{
    /// <summary>
    /// Phases of an NPC's home schedule. Plain enum, lives in Game.Data.
    ///
    /// Unity setup: none. Part of <see cref="NpcScheduleState"/>.
    /// </summary>
    public enum NpcSchedulePhase
    {
        /// <summary>Out and about (wandering).</summary>
        Away = 0,

        /// <summary>Walking to the home door.</summary>
        ToHome = 1,

        /// <summary>Inside the home.</summary>
        Home = 2,
    }
}
