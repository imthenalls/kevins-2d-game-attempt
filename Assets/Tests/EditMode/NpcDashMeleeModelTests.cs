using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the dash-melee phase machine. No scene or UnityEngine — proof the AI
    /// decision lives in Game.Data. All loops are bounded so a regression can never hang the runner.
    /// </summary>
    public class NpcDashMeleeModelTests
    {
        private static NpcDashMeleeConfig Config() => new NpcDashMeleeConfig
        {
            DashRange = 6f,
            WarningDuration = 0.5f,
            DashSpeed = 10f,
            StoppingDistance = 1f,
            RecoveryDuration = 1f,
            ApproachSpeed = 3f,
        };

        private static NpcDashDecision Tick(NpcDashMeleeModel m, float distance, float delta = 0.1f) =>
            m.Tick(delta, distance, 12f, 1f, 0f, 0.3f, true);

        private static NpcDashMeleeModel AtDash()
        {
            var m = new NpcDashMeleeModel(Config());
            Tick(m, 5f);       // Approach -> Warning
            Tick(m, 5f, 0.6f); // Warning duration reached -> Dash
            return m;
        }

        // Drives the Dash phase to completion, reporting movement, and returns the model.
        private static void AdvanceThroughDash(NpcDashMeleeModel m)
        {
            for (int i = 0; i < 50 && m.Phase == NpcDashPhase.Dash; i++)
            {
                NpcDashDecision d = Tick(m, 5f);
                m.ReportDashMoved(d.Distance, d.Distance);
            }
        }

        [Test]
        public void Approach_Within_DashRange_Enters_Warning()
        {
            var m = new NpcDashMeleeModel(Config());
            NpcDashDecision d = Tick(m, 5f);

            Assert.AreEqual(NpcDashPhase.Warning, m.Phase);
            Assert.IsTrue(d.WarningActive);
        }

        [Test]
        public void Approach_Within_DashRange_Without_Line_Of_Sight_Keeps_Approaching()
        {
            var m = new NpcDashMeleeModel(Config());
            NpcDashDecision d = m.Tick(0.1f, 5f, 12f, 1f, 0f, 0.3f, true, hasLineOfSight: false);

            Assert.AreEqual(NpcDashPhase.Approach, m.Phase, "no clear shot means no committed dash");
            Assert.AreEqual(NpcDashIntent.Approach, d.Intent);
        }

        [Test]
        public void Approach_Beyond_Range_Moves_Toward_Target()
        {
            var m = new NpcDashMeleeModel(Config());
            NpcDashDecision d = Tick(m, 9f); // within aggro (12), beyond dash range (6)

            Assert.AreEqual(NpcDashIntent.Approach, d.Intent);
            Assert.AreEqual(0.3f, d.Distance, 0.0001f); // ApproachSpeed * delta
        }

        [Test]
        public void Warning_Commits_Direction_And_Enters_Dash()
        {
            NpcDashMeleeModel m = AtDash();

            Assert.AreEqual(NpcDashPhase.Dash, m.Phase);
            Assert.AreEqual(1f, m.DashDirectionX, 0.0001f);
            Assert.AreEqual(0f, m.DashDirectionZ, 0.0001f);
        }

        [Test]
        public void Dash_Steps_Then_Swing_When_Distance_Is_Spent()
        {
            NpcDashMeleeModel m = AtDash();

            for (int i = 0; i < 50 && m.Phase == NpcDashPhase.Dash; i++)
            {
                NpcDashDecision d = Tick(m, 5f);
                Assert.AreEqual(NpcDashIntent.Dash, d.Intent);
                m.ReportDashMoved(d.Distance, d.Distance);
            }

            Assert.AreEqual(NpcDashPhase.Swing, m.Phase);
        }

        [Test]
        public void Blocked_Dash_Resumes_Approach_Instead_Of_Swinging()
        {
            NpcDashMeleeModel m = AtDash();
            NpcDashDecision d = Tick(m, 5f);
            m.ReportDashMoved(0f, d.Distance); // wall blocked the whole step

            Assert.AreEqual(NpcDashPhase.Approach, m.Phase,
                "a blocked dash must re-approach so navigation can route around the wall");
        }

        [Test]
        public void Partially_Blocked_Dash_Resumes_Approach()
        {
            NpcDashMeleeModel m = AtDash();
            NpcDashDecision d = Tick(m, 5f);
            m.ReportDashMoved(d.Distance * 0.5f, d.Distance); // wall cut the step short

            Assert.AreEqual(NpcDashPhase.Approach, m.Phase,
                "swinging at a wall that stopped the dash is pointless; reposition instead");
        }

        [Test]
        public void Warning_Aborts_When_Line_Of_Sight_Is_Lost()
        {
            var m = new NpcDashMeleeModel(Config());
            Tick(m, 5f); // Approach -> Warning

            NpcDashDecision d = m.Tick(0.1f, 5f, 12f, 1f, 0f, 0.3f, true, hasLineOfSight: false);

            Assert.AreEqual(NpcDashPhase.Approach, m.Phase,
                "losing sight during the telegraph must cancel the pending dash");
            Assert.AreEqual(NpcDashIntent.Approach, d.Intent);
        }

        [Test]
        public void Swing_Attacks_Once_Then_Recovers()
        {
            NpcDashMeleeModel m = AtDash();
            AdvanceThroughDash(m);
            Assert.AreEqual(NpcDashPhase.Swing, m.Phase);

            Assert.AreEqual(NpcDashIntent.Attack, Tick(m, 5f).Intent, "first swing tick attacks");

            for (int i = 0; i < 20 && m.Phase == NpcDashPhase.Swing; i++)
                Tick(m, 5f);

            Assert.AreEqual(NpcDashPhase.Recovery, m.Phase);
        }

        [Test]
        public void Recovery_Returns_To_Approach_And_Reset_Clears()
        {
            NpcDashMeleeModel m = AtDash();
            AdvanceThroughDash(m);

            for (int i = 0; i < 20 && m.Phase == NpcDashPhase.Swing; i++)
                Tick(m, 5f);
            Assert.AreEqual(NpcDashPhase.Recovery, m.Phase);

            for (int i = 0; i < 30 && m.Phase == NpcDashPhase.Recovery; i++)
                Tick(m, 5f);
            Assert.AreEqual(NpcDashPhase.Approach, m.Phase);

            m.Reset();
            Assert.AreEqual(NpcDashPhase.Approach, m.Phase);
            Assert.AreEqual(0f, m.PhaseTime, 0.0001f);
        }
    }
}
