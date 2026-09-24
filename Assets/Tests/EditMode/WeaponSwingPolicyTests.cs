using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the weapon swing hit-selection (reach + frontal cone).</summary>
    public class WeaponSwingPolicyTests
    {
        private const float Reach = 1.5f;
        private const float Cone = 75f;

        [Test]
        public void Target_Ahead_Is_Hit() =>
            Assert.IsTrue(WeaponSwingPolicy.IsInSwingCone(0f, 0f, 1f, 0f, 0f, Reach, Cone));

        [Test]
        public void Target_Behind_Is_Not_Hit() =>
            Assert.IsFalse(WeaponSwingPolicy.IsInSwingCone(0f, 0f, -1f, 0f, 0f, Reach, Cone));

        [Test]
        public void Target_Aligned_With_The_Blade_Is_Hit() =>
            Assert.IsTrue(WeaponSwingPolicy.IsInSwingCone(0f, 0f, 0f, 1f, -90f, Reach, Cone));

        [Test]
        public void Target_Inside_The_Cone_Is_Hit() =>
            Assert.IsTrue(WeaponSwingPolicy.IsInSwingCone(0f, 0f, 0.5f, -0.5f, 0f, Reach, Cone));

        [Test]
        public void Target_Outside_The_Cone_Is_Not_Hit() =>
            Assert.IsFalse(WeaponSwingPolicy.IsInSwingCone(0f, 0f, 0.5f, -0.5f, 130f, Reach, Cone));

        [Test]
        public void Target_Beyond_Reach_Is_Not_Hit() =>
            Assert.IsFalse(WeaponSwingPolicy.IsInSwingCone(0f, 0f, 2f, 0f, 0f, Reach, Cone));

        [Test]
        public void Target_On_The_Attacker_Is_Hit() =>
            Assert.IsTrue(WeaponSwingPolicy.IsInSwingCone(0f, 0f, 0f, 0f, 90f, Reach, Cone));

        [Test]
        public void Angle_Wraps_Across_Zero() =>
            Assert.IsTrue(WeaponSwingPolicy.IsInSwingCone(0f, 0f, 1f, 0f, 350f, Reach, Cone));
    }
}
