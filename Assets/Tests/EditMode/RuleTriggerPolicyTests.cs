using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the Enter/Exit/Both + one-shot trigger policy, including the stable-id
    /// fired key used to persist one-shots across reloads.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class RuleTriggerPolicyTests
    {
        [Test]
        public void Enter_Mode_Fires_On_Enter_Only()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: true, oneShot: false, out bool value));
            Assert.IsTrue(value);

            Assert.IsFalse(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: false, oneShot: false, out _));
        }

        [Test]
        public void Exit_Mode_Fires_On_Exit_Only()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsFalse(policy.TryResolve(RuleTriggerFireOn.Exit, false, entered: true, oneShot: false, out _));
            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Exit, false, entered: false, oneShot: false, out bool value));
            Assert.IsFalse(value);
        }

        [Test]
        public void Both_Mode_Inverts_The_Value_On_Exit()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Both, true, entered: true, oneShot: false, out bool onEnter));
            Assert.IsTrue(onEnter);

            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Both, true, entered: false, oneShot: false, out bool onExit));
            Assert.IsFalse(onExit);
        }

        [Test]
        public void One_Shot_Fires_Once_Until_Reset()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));
            Assert.IsTrue(policy.HasFired);
            Assert.IsFalse(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));

            policy.Reset(); // component re-enable
            Assert.IsFalse(policy.HasFired);
            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));
        }

        [Test]
        public void Repeatable_Trigger_Fires_Every_Enter()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Enter, false, entered: true, oneShot: false, out _));
            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Enter, false, entered: true, oneShot: false, out _));
        }

        [Test]
        public void Irrelevant_Events_Do_Not_Mark_The_Trigger_Fired()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsFalse(policy.TryResolve(RuleTriggerFireOn.Exit, true, entered: true, oneShot: true, out _));
            Assert.IsFalse(policy.HasFired);

            Assert.IsFalse(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: false, oneShot: true, out _));
            Assert.IsFalse(policy.HasFired);
        }

        [Test]
        public void Seeded_Fired_State_Suppresses_A_Persisted_One_Shot()
        {
            var policy = new RuleTriggerPolicy();
            policy.SeedFired(true);

            Assert.IsTrue(policy.HasFired);
            Assert.IsFalse(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));
        }

        [Test]
        public void Pending_Resolve_Does_Not_Consume_A_One_Shot_Until_Committed()
        {
            var policy = new RuleTriggerPolicy();

            // Resolve without committing (as an adapter does while applying the rule).
            Assert.IsTrue(policy.TryResolvePending(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out bool value));
            Assert.IsTrue(value);
            Assert.IsFalse(policy.HasFired, "resolving must not consume the one-shot before it is applied");

            // Because it was never committed, the trigger can still fire on a retry.
            Assert.IsTrue(policy.TryResolvePending(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));

            // Commit only after a successful application; now it is consumed.
            policy.CommitFired();
            Assert.IsTrue(policy.HasFired);
            Assert.IsFalse(policy.TryResolvePending(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));
        }

        [Test]
        public void TryResolve_Still_Commits_In_One_Call()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsTrue(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));
            Assert.IsTrue(policy.HasFired);
            Assert.IsFalse(policy.TryResolve(RuleTriggerFireOn.Enter, true, entered: true, oneShot: true, out _));
        }

        [Test]
        public void Pending_Resolve_Ignores_Irrelevant_Events_Without_Committing()
        {
            var policy = new RuleTriggerPolicy();

            Assert.IsFalse(policy.TryResolvePending(RuleTriggerFireOn.Exit, true, entered: true, oneShot: true, out _));
            Assert.IsFalse(policy.TryResolvePending(RuleTriggerFireOn.Enter, true, entered: false, oneShot: true, out _));
            Assert.IsFalse(policy.HasFired);
        }

        [Test]
        public void Fired_Key_Is_Stable_And_Survives_A_Facts_Round_Trip()
        {
            Assert.AreEqual("ruleTrigger.fired.chest", RuleTriggerPolicy.FiredKey("chest"));

            var facts = new WorldFacts();
            facts.SetFlag(RuleTriggerPolicy.FiredKey("chest"));
            System.Collections.Generic.Dictionary<string, object> saved = facts.GetSnapshot();

            var restored = new WorldFacts();
            restored.LoadSnapshot(saved);

            var policy = new RuleTriggerPolicy();
            policy.SeedFired(restored.HasFlag(RuleTriggerPolicy.FiredKey("chest")));
            Assert.IsTrue(policy.HasFired);
        }
    }
}
