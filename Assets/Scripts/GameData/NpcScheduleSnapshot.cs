namespace Game.Core
{
    /// <summary>
    /// Immutable capture of one NPC's schedule state for saving. Plain struct, lives in Game.Data.
    ///
    /// Unity setup: none.
    /// </summary>
    public readonly struct NpcScheduleSnapshot
    {
        public readonly string NpcId;
        public readonly NpcSchedulePhase Phase;
        public readonly float SecondsRemaining;

        public NpcScheduleSnapshot(string npcId, NpcSchedulePhase phase, float secondsRemaining)
        {
            NpcId = npcId;
            Phase = phase;
            SecondsRemaining = secondsRemaining;
        }
    }
}
