using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free shared dash rules for the 2D and 3D player controllers: charge count, recharge
    /// timing, cooldown, dash duration, and start eligibility. The dimension-specific controllers
    /// supply per-frame elapsed time, a dash request, and the dash duration (distance / speed); the
    /// model returns <see cref="PlayerDashCommand"/> values and owns every dash timer.
    ///
    /// Held by PlayerController2D and PlayerController3D so both dimensions share one implementation.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class PlayerDashModel
    {
        private readonly PlayerMovementConfig config;
        private float rechargeRemaining;

        public PlayerDashModel(PlayerMovementConfig config)
        {
            this.config = config ?? new PlayerMovementConfig();
            ResetCharges();
        }

        public bool IsDashing { get; private set; }

        /// <summary>Dash charges currently available.</summary>
        public int Charges { get; private set; }

        /// <summary>Seconds left in the active dash (0 when not dashing).</summary>
        public float DashTimeRemaining { get; private set; }

        /// <summary>Seconds until a new dash may start.</summary>
        public float CooldownRemaining { get; private set; }

        /// <summary>Effective charge cap (never below the configured minimum).</summary>
        public int MaxCharges => Math.Max(config.MinDashCharges, config.MaxDashCharges);

        public bool DashEnabled => config.DashEnabled;

        /// <summary>True when a dash may start right now.</summary>
        public bool CanStart =>
            DashEnabled && !IsDashing && Charges > 0 && CooldownRemaining <= 0f;

        /// <summary>Refills every charge and clears recharge timing (avatar/profile change).</summary>
        public void ResetCharges()
        {
            Charges = MaxCharges;
            rechargeRemaining = 0f;
        }

        /// <summary>Advances the cooldown and recharge timers. Call once per frame (Update).</summary>
        public void TickTimers(float deltaTime)
        {
            if (deltaTime < 0f) deltaTime = 0f;

            if (CooldownRemaining > 0f)
                CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaTime);

            Recharge(deltaTime);
        }

        /// <summary>
        /// Tries to start a dash lasting <paramref name="duration"/> seconds. Returns
        /// <see cref="PlayerDashCommand.StartDash"/> when it starts, otherwise
        /// <see cref="PlayerDashCommand.None"/>.
        /// </summary>
        public PlayerDashCommand RequestDash(float duration)
        {
            if (!CanStart) return PlayerDashCommand.None;

            bool wasFullyCharged = Charges == MaxCharges;
            Charges--;
            if (wasFullyCharged)
                rechargeRemaining = config.DashRechargeSeconds;

            DashTimeRemaining = Math.Max(0f, duration);
            CooldownRemaining = config.DashCooldown;
            IsDashing = true;
            return PlayerDashCommand.StartDash;
        }

        /// <summary>
        /// Advances the active dash. Call once per physics step (FixedUpdate). Returns
        /// <see cref="PlayerDashCommand.ContinueDash"/> while it runs or
        /// <see cref="PlayerDashCommand.StopDash"/> on the step it ends.
        /// </summary>
        public PlayerDashCommand TickDash(float deltaTime)
        {
            if (!IsDashing) return PlayerDashCommand.None;
            if (deltaTime < 0f) deltaTime = 0f;

            DashTimeRemaining -= deltaTime;
            if (DashTimeRemaining <= 0f)
            {
                DashTimeRemaining = 0f;
                IsDashing = false;
                return PlayerDashCommand.StopDash;
            }

            return PlayerDashCommand.ContinueDash;
        }

        /// <summary>Interrupts the current dash (movement disabled or dash cancelled).</summary>
        public void CancelDash()
        {
            IsDashing = false;
            DashTimeRemaining = 0f;
        }

        /// <summary>
        /// Fraction of a fixed step still covered by the dash, so the final partial step moves less.
        /// Returns 0 when not dashing.
        /// </summary>
        public float DashStepFraction(float stepDeltaTime)
        {
            if (!IsDashing || stepDeltaTime <= 0f) return 0f;

            float fraction = DashTimeRemaining / stepDeltaTime;
            if (fraction < 0f) return 0f;
            if (fraction > 1f) return 1f;
            return fraction;
        }

        private void Recharge(float deltaTime)
        {
            if (Charges >= MaxCharges)
            {
                Charges = MaxCharges;
                rechargeRemaining = 0f;
                return;
            }

            rechargeRemaining -= deltaTime;
            while (rechargeRemaining <= 0f && Charges < MaxCharges)
            {
                Charges++;
                if (Charges < MaxCharges)
                    rechargeRemaining += Math.Max(config.MinDashRechargeInterval, config.DashRechargeSeconds);
                else
                    rechargeRemaining = 0f;
            }
        }
    }
}
