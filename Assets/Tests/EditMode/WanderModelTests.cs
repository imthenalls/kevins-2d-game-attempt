using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the wander decision model (targets, idle, arrival, stall).</summary>
    public class WanderModelTests
    {
        private static WanderModel Model() => new WanderModel(3f, 0.2f, 0.5f, 1f, 12345);

        [Test]
        public void Candidates_Are_Within_Radius_And_Attempts_Are_Bounded()
        {
            WanderModel m = Model();
            int count = 0;

            while (m.TryNextCandidate(0f, 0f, out float x, out float z))
            {
                count++;
                float distance = (float)System.Math.Sqrt(x * x + z * z);
                Assert.LessOrEqual(distance, 3.001f);
            }

            Assert.AreEqual(WanderModel.MaxCandidateAttempts, count);
        }

        [Test]
        public void Idle_Begins_And_Ticks_Down()
        {
            WanderModel m = Model();
            m.BeginIdle();

            Assert.IsTrue(m.IsIdle);
            m.TickIdle(0.5f);
            Assert.IsTrue(m.IsIdle);
            m.TickIdle(1f);
            Assert.IsFalse(m.IsIdle);
        }

        [Test]
        public void Evaluate_Reports_Arrival_Within_Threshold()
        {
            WanderModel m = Model();

            m.BeginTarget(1f, 0f, 0f, 0f);
            Assert.AreEqual(WanderDecision.Moving, m.Evaluate(0f, 0f, 0.1f));

            m.BeginTarget(0.1f, 0f, 0f, 0f);
            Assert.AreEqual(WanderDecision.Arrived, m.Evaluate(0f, 0f, 0.1f));
        }

        [Test]
        public void Evaluate_Reports_Stall_After_Timeout_Without_Progress()
        {
            WanderModel m = Model();
            m.BeginTarget(5f, 0f, 0f, 0f);

            WanderDecision decision = WanderDecision.Moving;
            for (int i = 0; i < 10 && decision == WanderDecision.Moving; i++)
                decision = m.Evaluate(0f, 0f, 0.1f);

            Assert.AreEqual(WanderDecision.Stalled, decision);
        }

        [Test]
        public void Progress_Resets_The_Stall_Timer()
        {
            WanderModel m = Model();
            m.BeginTarget(50f, 0f, 0f, 0f);

            // Move a little each tick so the stall timer never matures.
            float x = 0f;
            for (int i = 0; i < 10; i++)
            {
                x += 0.1f;
                Assert.AreEqual(WanderDecision.Moving, m.Evaluate(x, 0f, 0.1f));
            }
        }

        [Test]
        public void Direction_And_Distance_Reflect_The_Target()
        {
            WanderModel m = Model();
            m.BeginTarget(0f, 3f, 0f, 0f);

            Assert.IsTrue(m.TryGetDirection(0f, 0f, out float dx, out float dz));
            Assert.AreEqual(0f, dx, 0.0001f);
            Assert.AreEqual(1f, dz, 0.0001f);
            Assert.AreEqual(3f, m.DistanceToTarget(0f, 0f), 0.001f);
        }

        [Test]
        public void AbortTarget_Clears_The_Target()
        {
            WanderModel m = Model();
            m.BeginTarget(1f, 1f, 0f, 0f);
            Assert.IsTrue(m.HasTarget);

            m.AbortTarget();
            Assert.IsFalse(m.HasTarget);
        }
    }
}
