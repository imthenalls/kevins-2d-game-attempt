using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative, saveable schedule state machine for one NPC. It owns the phase, its timer, and
    /// every transition: Away→ToHome→Home, portal success/failure, retry timing, and the randomized
    /// first Away leg. Inputs are timer expiries (via <see cref="Tick"/>) and
    /// <see cref="NpcScheduleEvent"/> reports from the facade; the output is an
    /// <see cref="NpcScheduleCommand"/> the MonoBehaviour executes. No Unity dependency, so the whole
    /// schedule is unit-tested without a scene.
    ///
    /// Unity setup: none. Created and owned by GameSession (via NpcScheduleRepository); the
    /// NpcSchedule3D MonoBehaviour is a thin facade that performs the returned Unity operations.
    ///
    /// Runtime API: Phase, SecondsRemaining, Tick, Handle, Restore, IsHome, Changed.
    /// </summary>
    public sealed class NpcScheduleState
    {
        public string NpcId { get; }
        public NpcSchedulePhase Phase { get; private set; }
        public float SecondsRemaining { get; private set; }

        /// <summary>Fired when the phase or its timer changes. Tick does not raise it.</summary>
        public event Action<NpcScheduleState> Changed;

        public NpcScheduleState(string npcId, NpcSchedulePhase phase, float secondsRemaining)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException("A stable npcId is required.", nameof(npcId));

            NpcId = npcId;
            Phase = phase;
            SecondsRemaining = Math.Max(0f, secondsRemaining);
        }

        public bool IsHome => Phase == NpcSchedulePhase.Home;

        /// <summary>
        /// Advances the phase timer. Returns the command produced by an expiry: Away → RequestRoute
        /// (and the phase becomes ToHome), Home → LeaveHomePortal. Returns None otherwise. Called every
        /// frame, so it does not raise <see cref="Changed"/>.
        /// </summary>
        public NpcScheduleCommand Tick(float deltaSeconds, NpcScheduleConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (deltaSeconds <= 0f || SecondsRemaining <= 0f)
                return NpcScheduleCommand.None;

            SecondsRemaining = Math.Max(0f, SecondsRemaining - deltaSeconds);
            if (SecondsRemaining > 0f)
                return NpcScheduleCommand.None;

            switch (Phase)
            {
                case NpcSchedulePhase.Away:
                    SetPhase(NpcSchedulePhase.ToHome, 0f);
                    return NpcScheduleCommand.RequestRoute;

                case NpcSchedulePhase.Home:
                    return NpcScheduleCommand.LeaveHomePortal;

                default:
                    return NpcScheduleCommand.None;
            }
        }

        /// <summary>Applies a facade-reported event and returns the resulting command.</summary>
        public NpcScheduleCommand Handle(NpcScheduleEvent scheduleEvent, NpcScheduleConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            switch (Phase)
            {
                case NpcSchedulePhase.ToHome:
                    switch (scheduleEvent)
                    {
                        case NpcScheduleEvent.RouteFailed:
                            SetPhase(NpcSchedulePhase.Away, config.RetrySeconds);
                            return NpcScheduleCommand.CloseHomeDoor;

                        case NpcScheduleEvent.ReachedEntrance:
                            return NpcScheduleCommand.EnterHomePortal;

                        case NpcScheduleEvent.PortalSucceeded:
                            SetPhase(NpcSchedulePhase.Home, config.HomeSeconds);
                            return NpcScheduleCommand.OpenHomeDoor;

                        case NpcScheduleEvent.PortalFailed:
                            SetPhase(NpcSchedulePhase.Away, config.AwaySeconds);
                            return NpcScheduleCommand.CloseHomeDoor;
                    }

                    break;

                case NpcSchedulePhase.Home:
                    switch (scheduleEvent)
                    {
                        case NpcScheduleEvent.PortalSucceeded:
                            SetPhase(NpcSchedulePhase.Away, config.AwaySeconds);
                            return NpcScheduleCommand.CloseHomeDoor;

                        case NpcScheduleEvent.PortalFailed:
                            // Still inside; stay Home and try again after the retry delay.
                            SetPhase(NpcSchedulePhase.Home, config.RetrySeconds);
                            return NpcScheduleCommand.None;
                    }

                    break;
            }

            return NpcScheduleCommand.None;
        }

        /// <summary>Restores a saved snapshot (used by load).</summary>
        public void Restore(NpcSchedulePhase phase, float secondsRemaining)
        {
            Phase = phase;
            SecondsRemaining = Math.Max(0f, secondsRemaining);
            Changed?.Invoke(this);
        }

        private void SetPhase(NpcSchedulePhase phase, float secondsRemaining)
        {
            float next = Math.Max(0f, secondsRemaining);
            if (phase == Phase && Math.Abs(next - SecondsRemaining) < 0.0001f)
                return;

            Phase = phase;
            SecondsRemaining = next;
            Changed?.Invoke(this);
        }
    }
}
