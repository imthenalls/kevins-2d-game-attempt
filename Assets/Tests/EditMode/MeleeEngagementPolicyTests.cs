using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the proximity-melee engagement decision: target-alive, behavior-blocked,
    /// and distance-based intents.
    /// </summary>
    public class MeleeEngagementPolicyTests
    {
        private static MeleeEngagement Evaluate(float distance, float attackRange, float disengageMultiplier) =>
            MeleeEngagementPolicy.Evaluate(
                distance, attackRange, disengageMultiplier,
                targetAlive: true, NpcBehaviorState.Idle);

        [Test]
        public void In_Range_Attacks() =>
            Assert.AreEqual(MeleeEngagement.Attack, Evaluate(1.0f, 1.5f, 4f));

        [Test]
        public void Beyond_Range_But_Within_Disengage_Chases()
        {
            Assert.AreEqual(MeleeEngagement.Chase, Evaluate(3.0f, 1.5f, 4f));
            Assert.AreEqual(MeleeEngagement.Chase, Evaluate(6.0f, 1.5f, 4f));
        }

        [Test]
        public void Beyond_Disengage_Range_Disengages() =>
            Assert.AreEqual(MeleeEngagement.Disengage, Evaluate(6.1f, 1.5f, 4f));

        [Test]
        public void Zero_Range_Disengages() =>
            Assert.AreEqual(MeleeEngagement.Disengage, Evaluate(0.5f, 0f, 4f));

        [Test]
        public void Dead_Target_Is_Idle() =>
            Assert.AreEqual(
                MeleeEngagement.Idle,
                MeleeEngagementPolicy.Evaluate(1.0f, 1.5f, 4f, targetAlive: false, NpcBehaviorState.Idle));

        [Test]
        public void Talking_Or_Disabled_Is_Blocked()
        {
            Assert.AreEqual(
                MeleeEngagement.Blocked,
                MeleeEngagementPolicy.Evaluate(1.0f, 1.5f, 4f, targetAlive: true, NpcBehaviorState.Talking));
            Assert.AreEqual(
                MeleeEngagement.Blocked,
                MeleeEngagementPolicy.Evaluate(1.0f, 1.5f, 4f, targetAlive: true, NpcBehaviorState.Disabled));
        }

        [Test]
        public void Dead_Target_Takes_Precedence_Over_Blocked_State() =>
            Assert.AreEqual(
                MeleeEngagement.Idle,
                MeleeEngagementPolicy.Evaluate(1.0f, 1.5f, 4f, targetAlive: false, NpcBehaviorState.Talking));
    }
}
