using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free melee attack state machine: cooldown, the visual/damage window, input buffering,
    /// the once-per-swing hit registry, and the once-per-swing recoil flag. Holds no Unity types
    /// (the hit registry keys on <see cref="object"/> references supplied by the adapter). Plain C#,
    /// lives in Game.Data and is unit-testable without a scene.
    ///
    /// Unity setup: none. Constructed by CombatAttacker with its CombatAttackerConfig.
    /// </summary>
    public sealed class AttackModel
    {
        private readonly CombatAttackerConfig config;
        private readonly HashSet<object> hitTargets = new HashSet<object>();

        private float cooldownTimer;
        private float animationTimer;
        private bool hasBuffered;
        private bool recoilApplied;

        public bool IsWeaponHitWindowOpen { get; private set; }
        public float AttackDuration => config.AttackDuration;
        public float AttackRange => config.AttackRange;

        public AttackModel(CombatAttackerConfig config)
        {
            this.config = config ?? new CombatAttackerConfig();
        }

        public void Reset()
        {
            cooldownTimer = 0f;
            animationTimer = 0f;
            hasBuffered = false;
            recoilApplied = false;
            IsWeaponHitWindowOpen = false;
            hitTargets.Clear();
        }

        /// <summary>
        /// Advances timers. Returns true when a buffered follow-up swing begins this tick.
        /// </summary>
        public bool Tick(float delta, bool hasWeapon)
        {
            if (cooldownTimer > 0f)
                cooldownTimer -= delta;

            if (animationTimer > 0f)
            {
                animationTimer -= delta;
                if (animationTimer <= 0f)
                {
                    animationTimer = 0f;
                    IsWeaponHitWindowOpen = false;

                    bool began = hasBuffered && hasWeapon;
                    hasBuffered = false;
                    if (began)
                    {
                        Begin();
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Begins a swing if ready and a weapon is available. Returns true when it begins.</summary>
        public bool TryBegin(bool hasWeapon)
        {
            if (cooldownTimer > 0f || animationTimer > 0f || !hasWeapon)
                return false;

            Begin();
            return true;
        }

        /// <summary>
        /// Player input path: begin now, or queue one follow-up during the final buffer window.
        /// Returns true when a swing begins immediately.
        /// </summary>
        public bool HandleInput(bool hasWeapon)
        {
            if (!hasWeapon)
            {
                hasBuffered = false;
                return false;
            }

            if (cooldownTimer <= 0f && animationTimer <= 0f)
            {
                Begin();
                return true;
            }

            if (animationTimer <= 0f || hasBuffered)
                return false;

            float duration = Max(0.01f, config.AttackDuration);
            float progress = 1f - Clamp01(animationTimer / duration);
            float bufferStart = 1f - Clamp01(config.AttackBufferWindow);
            if (progress >= bufferStart)
                hasBuffered = true;
            return false;
        }

        /// <summary>
        /// Registers a candidate hit during the open window. Returns false when the window is closed
        /// or the target was already hit this swing.
        /// </summary>
        public bool TryRegisterHit(object target, bool canHitSelf, object self)
        {
            if (!IsWeaponHitWindowOpen || target == null)
                return false;
            if (!canHitSelf && ReferenceEquals(target, self))
                return false;

            return hitTargets.Add(target);
        }

        /// <summary>True once per swing, for applying self-recoil on the first landed hit.</summary>
        public bool TryConsumeRecoil()
        {
            if (recoilApplied)
                return false;

            recoilApplied = true;
            return true;
        }

        private void Begin()
        {
            hasBuffered = false;
            hitTargets.Clear();
            recoilApplied = false;
            cooldownTimer = Max(config.AttackCooldown, config.AttackDuration);
            animationTimer = config.AttackDuration;
            IsWeaponHitWindowOpen = true;
        }

        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
        private static float Max(float a, float b) => a > b ? a : b;
    }
}
