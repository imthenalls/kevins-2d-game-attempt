using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the proximity-melee engagement decision.</summary>
    public class MeleeEngagementPolicyTests
    {
        [Test]
        public void In_Range_Attacks() =>
            Assert.AreEqual(MeleeEngagement.Attack, MeleeEngagementPolicy.Evaluate(1.0f, 1.5f, 4f));

        [Test]
        public void Beyond_Range_But_Within_Disengage_Chases()
        {
            Assert.AreEqual(MeleeEngagement.Chase, MeleeEngagementPolicy.Evaluate(3.0f, 1.5f, 4f));
            Assert.AreEqual(MeleeEngagement.Chase, MeleeEngagementPolicy.Evaluate(6.0f, 1.5f, 4f));
        }

        [Test]
        public void Beyond_Disengage_Range_Disengages() =>
            Assert.AreEqual(MeleeEngagement.Disengage, MeleeEngagementPolicy.Evaluate(6.1f, 1.5f, 4f));

        [Test]
        public void Zero_Range_Disengages() =>
            Assert.AreEqual(MeleeEngagement.Disengage, MeleeEngagementPolicy.Evaluate(0.5f, 0f, 4f));
    }
}
