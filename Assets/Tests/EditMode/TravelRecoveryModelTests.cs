using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the NPC travel-recovery policy: stall detection, bounded repaths, and
    /// abandonment. This is the recovery shared by home commuting and wandering.
    /// </summary>
    public class TravelRecoveryModelTests
    {
        [Test]
        public void Progress_Reports_Moving()
        {
            var m = new TravelRecoveryModel(0.5f, 2);
            m.Reset(0f, 0f);

            float x = 0f;
            for (int i = 0; i < 5; i++)
            {
                x += 0.1f;
                Assert.AreEqual(TravelRecoveryDecision.Moving, m.Evaluate(x, 0f, 0.2f, atWaypoint: false));
            }
        }

        [Test]
        public void Waypoint_Reports_Arrived_And_Clears_Stall()
        {
            var m = new TravelRecoveryModel(0.5f, 2);
            m.Reset(0f, 0f);

            // Burn almost all of the stall window, then arrive.
            m.Evaluate(0f, 0f, 0.4f, atWaypoint: false);
            Assert.AreEqual(TravelRecoveryDecision.Arrived, m.Evaluate(0f, 0f, 0.4f, atWaypoint: true));

            // Stall timer restarted, so a short pause must not trigger a repath yet.
            Assert.AreEqual(TravelRecoveryDecision.Moving, m.Evaluate(0f, 0f, 0.2f, atWaypoint: false));
        }

        [Test]
        public void No_Progress_Repaths_Then_Abandons()
        {
            var m = new TravelRecoveryModel(0.5f, 2);
            m.Reset(0f, 0f);

            Assert.AreEqual(TravelRecoveryDecision.Repath, DriveUntilNotMoving(m, out _));
            Assert.AreEqual(1, m.Repaths);
            Assert.AreEqual(TravelRecoveryDecision.Repath, DriveUntilNotMoving(m, out _));
            Assert.AreEqual(2, m.Repaths);
            Assert.AreEqual(TravelRecoveryDecision.Abandon, DriveUntilNotMoving(m, out _));
            Assert.AreEqual(2, m.Repaths);
        }

        [Test]
        public void Reset_Clears_The_Repath_Budget()
        {
            var m = new TravelRecoveryModel(0.5f, 1);
            m.Reset(0f, 0f);

            Assert.AreEqual(TravelRecoveryDecision.Repath, DriveUntilNotMoving(m, out _));

            m.Reset(0f, 0f);
            Assert.AreEqual(0, m.Repaths);
            Assert.AreEqual(TravelRecoveryDecision.Repath, DriveUntilNotMoving(m, out _));
        }

        [Test]
        public void Zero_Repaths_Abandons_On_First_Stall()
        {
            var m = new TravelRecoveryModel(0.5f, 0);
            m.Reset(0f, 0f);

            Assert.AreEqual(TravelRecoveryDecision.Abandon, DriveUntilNotMoving(m, out _));
        }

        // Ticks a stationary agent until it stops reporting Moving, returning that decision.
        private static TravelRecoveryDecision DriveUntilNotMoving(TravelRecoveryModel m, out int ticks)
        {
            TravelRecoveryDecision decision = TravelRecoveryDecision.Moving;
            ticks = 0;
            for (int i = 0; i < 50 && decision == TravelRecoveryDecision.Moving; i++)
            {
                ticks++;
                decision = m.Evaluate(0f, 0f, 0.1f, atWaypoint: false);
            }

            return decision;
        }
    }
}
