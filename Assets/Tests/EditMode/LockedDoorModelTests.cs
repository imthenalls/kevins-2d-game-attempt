using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the 3D door lock/state model.</summary>
    public class LockedDoorModelTests
    {
        private static Door3DConfig Config(string keyId, bool startsOpen = false)
        {
            return new Door3DConfig
            {
                RequiredKeyId = keyId,
                StartsOpen = startsOpen,
                RemainUnlocked = true,
            };
        }

        [Test]
        public void Door_With_No_Key_Is_Unlocked_And_Opens()
        {
            var model = new LockedDoorModel(Config(""));
            Assert.IsFalse(model.IsLocked);
            Assert.AreEqual(GateUseResult.Opened, model.ResolveUse(holderHasKey: false));
        }

        [Test]
        public void Locked_Door_Without_The_Key_Stays_Locked()
        {
            var model = new LockedDoorModel(Config("wood_shop_key"));
            Assert.IsTrue(model.IsLocked);
            Assert.AreEqual(GateUseResult.Locked, model.ResolveUse(holderHasKey: false));
        }

        [Test]
        public void Locked_Door_With_The_Key_Opens_And_Remains_Unlocked()
        {
            var model = new LockedDoorModel(Config("wood_shop_key"));
            Assert.AreEqual(GateUseResult.Opened, model.ResolveUse(holderHasKey: true));
            Assert.IsFalse(model.IsLocked, "RemainUnlocked should clear the lock after a successful open.");
            Assert.AreEqual(GateUseResult.Opened, model.ResolveUse(holderHasKey: false));
        }

        [Test]
        public void ApplyOpen_Tracks_The_Physical_State()
        {
            var model = new LockedDoorModel(Config("wood_shop_key"));
            Assert.IsFalse(model.IsOpen);
            model.ApplyOpen(true);
            Assert.IsTrue(model.IsOpen);
            Assert.AreEqual(GateUseResult.Opened, model.ResolveUse(holderHasKey: false));
        }

        [Test]
        public void StartsOpen_Door_Is_Open_And_Unlocked()
        {
            var model = new LockedDoorModel(Config("wood_shop_key", startsOpen: true));
            Assert.IsTrue(model.IsOpen);
            Assert.IsFalse(model.IsLocked);
        }
    }
}
