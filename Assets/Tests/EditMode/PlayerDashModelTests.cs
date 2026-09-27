using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the shared player dash model used by both PlayerController2D and
    /// PlayerController3D: charges, cooldown, recharge, start eligibility, and dash timing.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class PlayerDashModelTests
    {
        private static PlayerMovementConfig Config(int maxCharges = 2)
        {
            return new PlayerMovementConfig
            {
                DashEnabled = true,
                MaxDashCharges = maxCharges,
                MinDashCharges = 1,
                DashCooldown = 1f,
                DashRechargeSeconds = 5f,
                MinDashRechargeSeconds = 0.1f,
                MinDashRechargeInterval = 0.1f,
            };
        }

        [Test]
        public void Starts_Full_And_Consumes_A_Charge()
        {
            var model = new PlayerDashModel(Config());

            Assert.AreEqual(2, model.Charges);
            Assert.AreEqual(PlayerDashCommand.StartDash, model.RequestDash(0.5f));
            Assert.AreEqual(1, model.Charges);
            Assert.IsTrue(model.IsDashing);
        }

        [Test]
        public void Disabled_Dashing_Cannot_Start()
        {
            PlayerMovementConfig config = Config();
            config.DashEnabled = false;
            var model = new PlayerDashModel(config);

            Assert.IsFalse(model.CanStart);
            Assert.AreEqual(PlayerDashCommand.None, model.RequestDash(0.5f));
            Assert.AreEqual(2, model.Charges);
        }

        [Test]
        public void Cannot_Start_While_Already_Dashing()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(0.5f);

            Assert.IsFalse(model.CanStart);
            Assert.AreEqual(PlayerDashCommand.None, model.RequestDash(0.5f));
        }

        [Test]
        public void Cooldown_Blocks_Restart_Until_It_Expires()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(0.5f);
            model.TickDash(0.5f); // dash ends

            Assert.IsFalse(model.IsDashing);
            Assert.IsFalse(model.CanStart); // cooldown still running

            model.TickTimers(1f);
            Assert.IsTrue(model.CanStart);
        }

        [Test]
        public void Multiple_Charges_Are_Spent_Before_Recharge()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(0.1f);
            model.TickDash(0.1f);
            model.TickTimers(1f);

            Assert.AreEqual(PlayerDashCommand.StartDash, model.RequestDash(0.1f));
            Assert.AreEqual(0, model.Charges);
            Assert.IsFalse(model.CanStart);

            model.TickDash(0.1f);
            model.TickTimers(1f);
            Assert.IsFalse(model.CanStart); // no charges to spend
        }

        [Test]
        public void Recharge_Restores_A_Charge_After_The_Interval()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(0.1f); // from full: starts the recharge timer at 5s
            Assert.AreEqual(1, model.Charges);

            model.TickTimers(4f);
            Assert.AreEqual(1, model.Charges);

            model.TickTimers(1.01f);
            Assert.AreEqual(2, model.Charges);
        }

        [Test]
        public void TickDash_Reports_Continue_Then_Stop()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(1f);

            Assert.AreEqual(PlayerDashCommand.ContinueDash, model.TickDash(0.5f));
            Assert.AreEqual(PlayerDashCommand.StopDash, model.TickDash(0.6f));
            Assert.IsFalse(model.IsDashing);
            Assert.AreEqual(PlayerDashCommand.None, model.TickDash(0.5f));
        }

        [Test]
        public void DashStepFraction_Is_Clamped_To_A_Partial_Final_Step()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(0.04f);

            Assert.AreEqual(1f, model.DashStepFraction(0.02f), 0.0001f); // full step
            model.TickDash(0.03f);
            Assert.AreEqual(0.5f, model.DashStepFraction(0.02f), 0.0001f); // partial final step

            model.TickDash(0.02f);
            Assert.AreEqual(0f, model.DashStepFraction(0.02f), 0.0001f); // dash ended
        }

        [Test]
        public void CancelDash_Interrupts_Without_Refunding_The_Charge()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(1f);

            model.CancelDash();

            Assert.IsFalse(model.IsDashing);
            Assert.AreEqual(0f, model.DashTimeRemaining, 0.0001f);
            Assert.AreEqual(1, model.Charges);
        }

        [Test]
        public void ResetCharges_Refills_And_Clears_Recharge()
        {
            var model = new PlayerDashModel(Config());
            model.RequestDash(0.1f); // from full: arms recharge at 5s
            model.TickDash(0.1f);    // finish the dash
            model.TickTimers(1f);    // clear the cooldown (recharge now at 4s)
            Assert.AreEqual(1, model.Charges);

            model.ResetCharges();
            Assert.AreEqual(2, model.Charges);

            // Recharge timing was cleared, so a full interval from now is required to top up again.
            Assert.AreEqual(PlayerDashCommand.StartDash, model.RequestDash(0.1f));
            model.TickDash(0.1f);
            Assert.AreEqual(1, model.Charges);
            model.TickTimers(4f);
            Assert.AreEqual(1, model.Charges);
            model.TickTimers(1.01f);
            Assert.AreEqual(2, model.Charges);
        }
    }
}
