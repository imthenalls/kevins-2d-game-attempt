using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the portal access rule.</summary>
    public class PortalAccessPolicyTests
    {
        [Test]
        public void Both_Satisfied_Allows() =>
            Assert.AreEqual(PortalAccess.Allowed, PortalAccessPolicy.Evaluate(unlockedByWorldState: true, keySatisfied: true));

        [Test]
        public void Missing_World_State_Flag_Blocks() =>
            Assert.AreEqual(PortalAccess.LockedByWorldState, PortalAccessPolicy.Evaluate(false, true));

        [Test]
        public void Missing_Key_Blocks() =>
            Assert.AreEqual(PortalAccess.LockedByKey, PortalAccessPolicy.Evaluate(true, false));

        [Test]
        public void World_State_Is_Checked_Before_The_Key() =>
            Assert.AreEqual(PortalAccess.LockedByWorldState, PortalAccessPolicy.Evaluate(false, false));
    }
}
