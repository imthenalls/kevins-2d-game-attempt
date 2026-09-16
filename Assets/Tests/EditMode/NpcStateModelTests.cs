using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Edit-mode tests for the pure-C# NPC state model. These run without entering Play Mode and
    /// without loading a Unity scene, proving the authoritative state is engine-independent.
    ///
    /// Unity setup: none. Runs via the Test Runner (EditMode) or the Unity CLI run_tests command.
    /// </summary>
    public class NpcStateModelTests
    {
        private const string Id = "sword_guard";

        [Test]
        public void Damage_Clamps_And_Reports_Applied()
        {
            var state = new NpcState(Id, 30, 30, 0, 0);

            int applied = state.ApplyDamage(12);

            Assert.AreEqual(12, applied);
            Assert.AreEqual(18, state.Hp);
            Assert.IsTrue(state.IsAlive);
        }

        [Test]
        public void Damage_Never_Goes_Below_Zero_And_Kills()
        {
            var state = new NpcState(Id, 10, 10, 0, 0);

            int applied = state.ApplyDamage(999);

            Assert.AreEqual(10, applied);
            Assert.AreEqual(0, state.Hp);
            Assert.IsFalse(state.IsAlive);
        }

        [Test]
        public void Heal_Clamps_To_MaxHp_And_Does_Not_Revive_Dead()
        {
            var state = new NpcState(Id, 20, 5, 0, 0);

            Assert.AreEqual(10, state.Heal(10));
            Assert.AreEqual(15, state.Hp);

            state.SetHp(0);
            Assert.AreEqual(0, state.Heal(5));
            Assert.AreEqual(0, state.Hp);
        }

        [Test]
        public void MoveToCell_Updates_Logical_Position()
        {
            var state = new NpcState(Id, 30, 30, -1, -4);

            state.MoveToCell(3, 7);

            Assert.AreEqual(3, state.CellX);
            Assert.AreEqual(7, state.CellY);
        }

        [Test]
        public void Service_Commands_Change_The_Model()
        {
            var session = new GameSession();
            session.NpcStates.Register(Id, 30, 30, 0, 0);

            session.NpcStates.ApplyDamage(Id, 5);
            session.NpcStates.MoveToCell(Id, 4, -2);

            Assert.IsTrue(session.NpcStates.TryGet(Id, out NpcState state));
            Assert.AreEqual(25, state.Hp);
            Assert.AreEqual(4, state.CellX);
            Assert.AreEqual(-2, state.CellY);
        }

        [Test]
        public void Snapshot_RoundTrip_Restores_State()
        {
            var session = new GameSession();
            session.NpcStates.Register(Id, 30, 30, 0, 0);
            session.NpcStates.ApplyDamage(Id, 8);
            session.NpcStates.MoveToCell(Id, 6, 6);

            Assert.IsTrue(session.NpcStates.TryCapture(Id, out NpcStateSnapshot snapshot));

            // Simulate a fresh app launch: new session, apply the saved snapshot.
            var reloaded = new GameSession();
            reloaded.NpcStates.Apply(snapshot);

            Assert.IsTrue(reloaded.NpcStates.TryGet(Id, out NpcState restored));
            Assert.AreEqual(snapshot.Hp, restored.Hp);
            Assert.AreEqual(snapshot.MaxHp, restored.MaxHp);
            Assert.AreEqual(snapshot.CellX, restored.CellX);
            Assert.AreEqual(snapshot.CellY, restored.CellY);
        }

        [Test]
        public void Destroying_And_Recreating_A_View_Keeps_The_Model()
        {
            var session = new GameSession();
            session.NpcStates.Register(Id, 30, 30, 0, 0);

            // "View A" holds a reference to the model, then is destroyed (reference dropped).
            NpcState viewA = session.NpcStates.Register(Id, 30, 30, 0, 0);
            session.NpcStates.ApplyDamage(Id, 9);
            viewA = null;
            Assert.IsNull(viewA);

            // "View B" is created later and binds by the same stable id.
            NpcState viewB = session.NpcStates.Register(Id, 30, 30, 0, 0);

            Assert.AreEqual(21, viewB.Hp);
            Assert.AreEqual(0, viewB.CellX);
            Assert.AreEqual(0, viewB.CellY);
        }

        [Test]
        public void Model_Does_Not_Require_UnityEngine()
        {
            // The model type lives in Game.Core, which has noEngineReferences. This test compiling
            // and running without a scene is the proof.
            var state = new NpcState(Id, 5, 5, 0, 0);
            Assert.AreEqual(5, state.Hp);
        }
    }
}
