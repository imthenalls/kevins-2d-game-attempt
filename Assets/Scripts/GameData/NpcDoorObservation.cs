namespace Game.Core
{
    /// <summary>
    /// What the NPC door adapter observed this tick (gate presence/open/moving, whether the NPC is in
    /// interaction range, whether its route ran out, whether it arrived past the gate, and whether the
    /// shared stall detector fired). Fed to <see cref="NpcUseDoorModel.Tick"/>.
    ///
    /// Unity setup: none.
    /// </summary>
    public struct NpcDoorObservation
    {
        /// <summary>No eligible gate was found (or it was remembered as locked).</summary>
        public bool GateMissing;

        /// <summary>The gate is fully open.</summary>
        public bool GateOpen;

        /// <summary>The gate is mid-animation.</summary>
        public bool GateMoving;

        /// <summary>The NPC is close enough to interact with the gate.</summary>
        public bool InRange;

        /// <summary>The current route ran out of waypoints (approach or pass).</summary>
        public bool RouteExhausted;

        /// <summary>The pass-through target was reached (only relevant without a pass route).</summary>
        public bool ArrivedAtPassTarget;

        /// <summary>The shared recovery model detected no progress.</summary>
        public bool Stalled;
    }
}
