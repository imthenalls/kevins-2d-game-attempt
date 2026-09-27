namespace Game.Core
{
    /// <summary>
    /// Engine-free phase machine for the NPC use-door behavior. It owns the Approach → PassThrough
    /// transitions and the response to each door result (open, moving, locked, busy, unavailable),
    /// returning <see cref="NpcDoorCommand"/> values. The Unity adapter keeps the gate reference,
    /// pathfinding, raycasts, Rigidbody motion, and NpcMemory/quest side effects.
    ///
    /// Held by NpcUseDoorBehavior. Unity setup: none.
    /// </summary>
    public sealed class NpcUseDoorModel
    {
        public NpcDoorPhase Phase { get; private set; } = NpcDoorPhase.Approach;

        public bool IsComplete { get; private set; }

        /// <summary>Starts (or restarts) the behavior at the approach phase.</summary>
        public void Enter()
        {
            Phase = NpcDoorPhase.Approach;
            IsComplete = false;
        }

        /// <summary>
        /// Evaluates the tick observation and returns the operation to perform. The adapter must
        /// handle <see cref="NpcDoorCommand.AttemptUse"/> by calling the gate and then feeding the
        /// outcome to <see cref="ResolveUse"/>.
        /// </summary>
        public NpcDoorCommand Tick(NpcDoorObservation observation)
        {
            if (IsComplete)
                return NpcDoorCommand.None;

            if (observation.GateMissing)
                return Complete();

            if (Phase == NpcDoorPhase.PassThrough)
                return TickPassThrough(observation);

            if (observation.GateOpen)
                return BeginPass();

            if (observation.GateMoving)
                return NpcDoorCommand.Wait;

            if (observation.Stalled)
                return observation.InRange ? NpcDoorCommand.AttemptUse : Complete();

            if (observation.InRange)
                return NpcDoorCommand.AttemptUse;

            if (observation.RouteExhausted)
                return NpcDoorCommand.None;

            return NpcDoorCommand.FollowRoute;
        }

        /// <summary>Converts a gate-use outcome into the next command.</summary>
        public NpcDoorCommand ResolveUse(GateUseResult result)
        {
            if (IsComplete)
                return NpcDoorCommand.None;

            switch (result)
            {
                case GateUseResult.Opened:
                    return BeginPass();

                case GateUseResult.Busy:
                    return NpcDoorCommand.Wait;

                default:
                    // Locked or Unavailable: give up. The adapter records the locked gate.
                    return Complete();
            }
        }

        private NpcDoorCommand TickPassThrough(NpcDoorObservation observation)
        {
            if (observation.ArrivedAtPassTarget || observation.RouteExhausted || observation.Stalled)
                return Complete();

            return NpcDoorCommand.FollowRoute;
        }

        private NpcDoorCommand BeginPass()
        {
            Phase = NpcDoorPhase.PassThrough;
            return NpcDoorCommand.BeginPass;
        }

        private NpcDoorCommand Complete()
        {
            IsComplete = true;
            return NpcDoorCommand.Complete;
        }
    }
}
