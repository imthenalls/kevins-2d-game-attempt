namespace Game.Core
{
    /// <summary>
    /// Engine-free trigger policy: decides whether an enter/exit event should fire, which boolean
    /// value to apply (inverting on exit for <see cref="RuleTriggerFireOn.Both"/>), and tracks the
    /// one-shot "already fired" state. The Unity adapter owns collider callbacks, tag filtering, and
    /// the SceneRulesManager method calls; optional one-shot persistence uses stable ids plus
    /// <see cref="FiredKey"/> in WorldFacts.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class RuleTriggerPolicy
    {
        /// <summary>Fact-key prefix recording that a one-shot trigger with a stable id already fired.</summary>
        public const string FiredKeyPrefix = "ruleTrigger.fired.";

        private bool fired;

        /// <summary>True once the trigger has fired (for one-shot suppression).</summary>
        public bool HasFired => fired;

        /// <summary>Clears the fired flag (component re-enable, or a fresh activation).</summary>
        public void Reset() => fired = false;

        /// <summary>Seeds the fired flag, for example from a persisted one-shot.</summary>
        public void SeedFired(bool value) => fired = value;

        /// <summary>The WorldFacts key recording that the trigger with this id already fired.</summary>
        public static string FiredKey(string triggerId) => FiredKeyPrefix + triggerId;

        /// <summary>
        /// Resolves an enter/exit event <b>without</b> changing the fired flag. Returns true when the
        /// trigger should fire, with <paramref name="appliedValue"/> set to the value to apply.
        /// Committing is a separate step (<see cref="CommitFired"/>) so an adapter can apply the value
        /// first and only consume a one-shot once the application succeeded.
        /// </summary>
        public bool TryResolvePending(
            RuleTriggerFireOn fireOn,
            bool value,
            bool entered,
            bool oneShot,
            out bool appliedValue)
        {
            appliedValue = false;

            bool relevant = entered
                ? fireOn != RuleTriggerFireOn.Exit
                : fireOn != RuleTriggerFireOn.Enter;
            if (!relevant)
                return false;

            if (oneShot && fired)
                return false;

            appliedValue = entered
                ? value
                : fireOn == RuleTriggerFireOn.Both ? !value : value;

            return true;
        }

        /// <summary>
        /// Marks the trigger as fired. Call this only after the resolved value was successfully
        /// applied, so a failed application does not permanently consume a one-shot.
        /// </summary>
        public void CommitFired() => fired = true;

        /// <summary>
        /// Resolves an enter/exit event and immediately marks it fired. This all-in-one form is for
        /// callers with no separate apply step; adapters that apply a value should use
        /// <see cref="TryResolvePending"/> followed by <see cref="CommitFired"/>. One-shot triggers
        /// fire only once until <see cref="Reset"/>.
        /// </summary>
        public bool TryResolve(
            RuleTriggerFireOn fireOn,
            bool value,
            bool entered,
            bool oneShot,
            out bool appliedValue)
        {
            if (!TryResolvePending(fireOn, value, entered, oneShot, out appliedValue))
                return false;

            fired = true;
            return true;
        }
    }
}
