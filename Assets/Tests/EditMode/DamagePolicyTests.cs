using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the combat damage policy.</summary>
    public class DamagePolicyTests
    {
        [Test]
        public void CanReceive_Requires_Enabled_Not_Invincible_Alive()
        {
            Assert.IsTrue(DamagePolicy.CanReceive(true, false, true));
            Assert.IsFalse(DamagePolicy.CanReceive(false, false, true));
            Assert.IsFalse(DamagePolicy.CanReceive(true, true, true));
            Assert.IsFalse(DamagePolicy.CanReceive(true, false, false));
        }

        [Test]
        public void Resolve_Scales_And_Rounds_To_Nearest_Even()
        {
            Assert.AreEqual(15, DamagePolicy.Resolve(10, 1.5f));
            Assert.AreEqual(5, DamagePolicy.Resolve(10, 0.5f));
            Assert.AreEqual(20, DamagePolicy.Resolve(10, 2f));
            Assert.AreEqual(2, DamagePolicy.Resolve(5, 0.5f), "2.5 ties to even");
            Assert.AreEqual(2, DamagePolicy.Resolve(3, 0.5f), "1.5 ties to even");
        }

        [Test]
        public void Resolve_Never_Goes_Negative_And_Ignores_NonPositive_Raw()
        {
            Assert.AreEqual(0, DamagePolicy.Resolve(10, -1f));
            Assert.AreEqual(0, DamagePolicy.Resolve(0, 3f));
            Assert.AreEqual(0, DamagePolicy.Resolve(-5, 3f));
        }
    }
}
