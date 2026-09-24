using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the gate lock rule.</summary>
    public class DoorLockPolicyTests
    {
        [Test]
        public void Unlocked_Gate_Always_Opens() =>
            Assert.IsTrue(DoorLockPolicy.CanUnlock(isLocked: false, requiredKeyId: "key_a", holderHasKey: false));

        [Test]
        public void Locked_Gate_With_The_Key_Opens() =>
            Assert.IsTrue(DoorLockPolicy.CanUnlock(isLocked: true, requiredKeyId: "key_a", holderHasKey: true));

        [Test]
        public void Locked_Gate_Without_The_Key_Stays_Closed()
        {
            Assert.IsFalse(DoorLockPolicy.CanUnlock(true, "key_a", false));
            Assert.IsFalse(DoorLockPolicy.CanUnlock(true, "", true), "a lock with no key id stays closed");
            Assert.IsFalse(DoorLockPolicy.CanUnlock(true, "", false));
        }
    }
}
