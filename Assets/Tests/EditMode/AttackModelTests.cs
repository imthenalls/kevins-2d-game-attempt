using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the melee attack state machine (cooldown/window/buffer/hits).</summary>
    public class AttackModelTests
    {
        private static AttackModel Model() => new AttackModel(new CombatAttackerConfig
        {
            AttackDamage = 10,
            AttackCooldown = 0.5f,
            AttackDuration = 0.3f,
            AttackBufferWindow = 0.5f,
            CanHitSelf = false,
            SelfRecoilDamage = 0,
        });

        [Test]
        public void TryBegin_Needs_A_Weapon_And_Opens_The_Window()
        {
            AttackModel m = Model();

            Assert.IsFalse(m.IsWeaponHitWindowOpen);
            Assert.IsFalse(m.TryBegin(false));
            Assert.IsFalse(m.IsWeaponHitWindowOpen);

            Assert.IsTrue(m.TryBegin(true));
            Assert.IsTrue(m.IsWeaponHitWindowOpen);
        }

        [Test]
        public void TryBegin_Is_Blocked_Until_Cooldown_Expires()
        {
            AttackModel m = Model();
            m.TryBegin(true);

            Assert.IsFalse(m.TryBegin(true), "still swinging");

            m.Tick(1.0f, true); // animation + cooldown elapse
            Assert.IsFalse(m.IsWeaponHitWindowOpen);
            Assert.IsTrue(m.TryBegin(true));
        }

        [Test]
        public void Register_Hit_Only_While_Window_Open_And_Once_Per_Target()
        {
            AttackModel m = Model();
            var target = new object();
            var other = new object();

            Assert.IsFalse(m.TryRegisterHit(target, false, null), "window closed");

            m.TryBegin(true);
            Assert.IsTrue(m.TryRegisterHit(target, false, null));
            Assert.IsFalse(m.TryRegisterHit(target, false, null), "same target twice");
            Assert.IsTrue(m.TryRegisterHit(other, false, null));
        }

        [Test]
        public void Self_Hit_Rejected_Unless_Allowed()
        {
            AttackModel m = Model();
            m.TryBegin(true);
            var self = new object();

            Assert.IsFalse(m.TryRegisterHit(self, false, self));
            Assert.IsTrue(m.TryRegisterHit(self, true, self));
        }

        [Test]
        public void Recoil_Consumes_Once_Per_Swing()
        {
            AttackModel m = Model();
            m.TryBegin(true);

            Assert.IsTrue(m.TryConsumeRecoil());
            Assert.IsFalse(m.TryConsumeRecoil());
        }

        [Test]
        public void Buffered_Input_Starts_A_Followup_When_The_Swing_Ends()
        {
            AttackModel m = Model();
            m.TryBegin(true);

            m.Tick(0.2f, true);                  // into the buffer window
            Assert.IsFalse(m.HandleInput(true), "cannot begin yet");

            Assert.IsTrue(m.Tick(0.2f, true), "buffered swing should begin");
            Assert.IsTrue(m.IsWeaponHitWindowOpen);
        }

        [Test]
        public void Reset_Clears_Everything()
        {
            AttackModel m = Model();
            m.TryBegin(true);
            m.TryRegisterHit(new object(), false, null);

            m.Reset();

            Assert.IsFalse(m.IsWeaponHitWindowOpen);
            var target = new object();
            Assert.IsFalse(m.TryRegisterHit(target, false, null), "hit registry cleared");
        }
    }
}
