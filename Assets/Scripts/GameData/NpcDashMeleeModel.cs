using System;

namespace Game.Core
{
    /// <summary>Phases of a telegraph-dash melee attack.</summary>
    public enum NpcDashPhase
    {
        Approach = 0,
        Warning = 1,
        Dash = 2,
        Swing = 3,
        Recovery = 4,
    }

    /// <summary>What the dash-melee NPC should do this tick.</summary>
    public enum NpcDashIntent
    {
        None = 0,
        Approach = 1,
        Dash = 2,
        Attack = 3,
    }

    /// <summary>Result of one model tick: the phase, the intent, and any movement/direction.</summary>
    public readonly struct NpcDashDecision
    {
        public readonly NpcDashPhase Phase;
        public readonly NpcDashIntent Intent;
        public readonly float Distance;
        public readonly float DirectionX;
        public readonly float DirectionZ;

        /// <summary>True while the warning flash is active (body should be tinted).</summary>
        public readonly bool WarningActive;

        public NpcDashDecision(
            NpcDashPhase phase, NpcDashIntent intent,
            float distance, float directionX, float directionZ, bool warningActive)
        {
            Phase = phase;
            Intent = intent;
            Distance = distance;
            DirectionX = directionX;
            DirectionZ = directionZ;
            WarningActive = warningActive;
        }
    }

    /// <summary>
    /// Engine-free state machine for a telegraph-dash melee enemy: Approach → Warning (flash) → Dash
    /// (committed straight line) → Swing → Recovery. Holds phase, phase time, and the committed dash
    /// direction/remaining distance. The Unity adapter supplies geometry (distance, direction,
    /// attack duration, whether movement was blocked) and applies the movement/attack. Plain C#,
    /// lives in Game.Data and is unit-testable without a scene.
    ///
    /// Unity setup: none. Constructed by NpcDashMelee3D with a NpcDashMeleeConfig.
    /// </summary>
    public sealed class NpcDashMeleeModel
    {
        private readonly NpcDashMeleeConfig config;

        public NpcDashPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public float DashRemaining { get; private set; }
        public float DashDirectionX { get; private set; }
        public float DashDirectionZ { get; private set; }

        public NpcDashMeleeModel(NpcDashMeleeConfig config)
        {
            this.config = config ?? new NpcDashMeleeConfig();
            Reset();
        }

        public void Reset()
        {
            Phase = NpcDashPhase.Approach;
            PhaseTime = 0f;
            DashRemaining = 0f;
            DashDirectionX = 0f;
            DashDirectionZ = 0f;
        }

        /// <summary>
        /// Advances the machine one tick. <paramref name="distance"/> is the gap to the target,
        /// <paramref name="directionX/Z"/> the normalized direction to it, and
        /// <paramref name="attackDuration"/> the swing length (from CombatAttacker).
        /// </summary>
        public NpcDashDecision Tick(
            float delta, float distance, float aggroRange,
            float directionX, float directionZ, float attackDuration, bool canAttack,
            bool hasLineOfSight = true)
        {
            PhaseTime += delta;

            switch (Phase)
            {
                case NpcDashPhase.Approach:
                    // Only commit to the dash with a clear shot; otherwise keep approaching so the
                    // committed straight dash never fires into a wall (the adapter routes around it).
                    if (distance <= config.DashRange && hasLineOfSight)
                    {
                        Enter(NpcDashPhase.Warning);
                        return new NpcDashDecision(Phase, NpcDashIntent.None, 0f, 0f, 0f, true);
                    }

                    if (distance <= aggroRange)
                        return new NpcDashDecision(Phase, NpcDashIntent.Approach,
                            config.ApproachSpeed * delta, directionX, directionZ, false);

                    return new NpcDashDecision(Phase, NpcDashIntent.None, 0f, 0f, 0f, false);

                case NpcDashPhase.Warning:
                    // If the shot clears during the telegraph, abort back to approach instead of
                    // committing a dash into the wall the target just moved behind.
                    if (!hasLineOfSight)
                    {
                        Enter(NpcDashPhase.Approach);
                        return new NpcDashDecision(Phase, NpcDashIntent.Approach,
                            config.ApproachSpeed * delta, directionX, directionZ, false);
                    }

                    if (PhaseTime + 0.0001f >= config.WarningDuration)
                    {
                        // Aim is committed here; dodging afterward does not steer the dash.
                        DashDirectionX = directionX;
                        DashDirectionZ = directionZ;
                        DashRemaining = Clamp(distance - config.StoppingDistance, 0f, config.DashRange);
                        Enter(NpcDashPhase.Dash);
                    }

                    return new NpcDashDecision(Phase, NpcDashIntent.None, 0f, 0f, 0f,
                        Phase == NpcDashPhase.Warning);

                case NpcDashPhase.Dash:
                {
                    float step = Math.Min(DashRemaining, config.DashSpeed * delta);
                    return new NpcDashDecision(Phase, NpcDashIntent.Dash, step,
                        DashDirectionX, DashDirectionZ, false);
                }

                case NpcDashPhase.Swing:
                    if (PhaseTime <= delta + 0.0001f && canAttack)
                        return new NpcDashDecision(Phase, NpcDashIntent.Attack, 0f, 0f, 0f, false);

                    if (PhaseTime >= attackDuration + delta)
                        Enter(NpcDashPhase.Recovery);
                    return new NpcDashDecision(Phase, NpcDashIntent.None, 0f, 0f, 0f, false);

                case NpcDashPhase.Recovery:
                    if (PhaseTime >= config.RecoveryDuration)
                        Enter(NpcDashPhase.Approach);
                    return new NpcDashDecision(Phase, NpcDashIntent.None, 0f, 0f, 0f, false);
            }

            return new NpcDashDecision(Phase, NpcDashIntent.None, 0f, 0f, 0f, false);
        }

        /// <summary>Reports how far the dash actually moved (walls may cut it short).</summary>
        public void ReportDashMoved(float moved, float desired)
        {
            if (Phase != NpcDashPhase.Dash)
                return;

            DashRemaining -= moved;
            if (DashRemaining <= 0.01f)
            {
                Enter(NpcDashPhase.Swing);
                return;
            }

            // A wall cut the committed dash short. Swinging at the wall is pointless; go back to
            // approach so navigation can route around it instead of re-dashing into it forever.
            if (moved + 0.001f < desired)
                Enter(NpcDashPhase.Approach);
        }

        private void Enter(NpcDashPhase phase)
        {
            Phase = phase;
            PhaseTime = 0f;
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);
    }
}
