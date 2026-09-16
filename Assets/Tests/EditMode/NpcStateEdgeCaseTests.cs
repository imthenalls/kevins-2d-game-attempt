using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Edge-case and invariant tests for the pure-C# NPC model and its repository/service.
    /// Complements NpcStateModelTests (happy paths) with boundaries, no-ops, and event behaviour.
    ///
    /// Unity setup: none. Runs via the Test Runner (EditMode) or `dotnet test`.
    /// </summary>
    public class NpcStateEdgeCaseTests
    {
        private const string Id = "sword_guard";

        // ── Construction ─────────────────────────────────────────────────────────

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Constructor_Rejects_Missing_Id(string npcId)
        {
            Assert.Throws<ArgumentException>(() => new NpcState(npcId, 30, 30, 0, 0));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void Constructor_Forces_MaxHp_To_At_Least_One(int maxHp)
        {
            var s = new NpcState(Id, maxHp, 5, 0, 0);
            Assert.AreEqual(1, s.MaxHp);
        }

        [TestCase(999, 30, 30)]
        [TestCase(-5, 30, 0)]
        [TestCase(10, 30, 10)]
        public void Constructor_Clamps_Initial_Hp(int hp, int maxHp, int expected)
        {
            var s = new NpcState(Id, maxHp, hp, 0, 0);
            Assert.AreEqual(expected, s.Hp);
        }

        // ── Damage ───────────────────────────────────────────────────────────────

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(-999)]
        public void ApplyDamage_NonPositive_Is_NoOp(int amount)
        {
            var s = new NpcState(Id, 30, 30, 0, 0);
            Assert.AreEqual(0, s.ApplyDamage(amount));
            Assert.AreEqual(30, s.Hp);
        }

        [Test]
        public void ApplyDamage_While_Dead_Is_NoOp()
        {
            var s = new NpcState(Id, 10, 10, 0, 0);
            s.ApplyDamage(10);
            Assert.IsFalse(s.IsAlive);

            Assert.AreEqual(0, s.ApplyDamage(5));
            Assert.AreEqual(0, s.Hp);
        }

        [Test]
        public void ApplyDamage_Reports_Only_The_Amount_Actually_Removed()
        {
            var s = new NpcState(Id, 10, 10, 0, 0);
            Assert.AreEqual(10, s.ApplyDamage(999));
        }

        // ── Heal ─────────────────────────────────────────────────────────────────

        [TestCase(0)]
        [TestCase(-5)]
        public void Heal_NonPositive_Is_NoOp(int amount)
        {
            var s = new NpcState(Id, 30, 10, 0, 0);
            Assert.AreEqual(0, s.Heal(amount));
            Assert.AreEqual(10, s.Hp);
        }

        [Test]
        public void Heal_At_Capacity_Returns_Zero_And_Raises_No_Event()
        {
            var s = new NpcState(Id, 30, 30, 0, 0);
            int events = 0;
            s.Changed += _ => events++;

            Assert.AreEqual(0, s.Heal(10));
            Assert.AreEqual(0, events);
        }

        [Test]
        public void Heal_Over_Capacity_Clamps_And_Reports_Actual()
        {
            var s = new NpcState(Id, 30, 25, 0, 0);
            Assert.AreEqual(5, s.Heal(100));
            Assert.AreEqual(30, s.Hp);
        }

        // ── Max HP ───────────────────────────────────────────────────────────────

        [Test]
        public void SetMaxHp_Below_Current_Hp_Clamps_Hp_Down()
        {
            var s = new NpcState(Id, 30, 30, 0, 0);
            s.SetMaxHp(12);
            Assert.AreEqual(12, s.MaxHp);
            Assert.AreEqual(12, s.Hp);
        }

        [Test]
        public void SetMaxHp_To_Same_Value_Raises_No_Event()
        {
            var s = new NpcState(Id, 30, 30, 0, 0);
            int events = 0;
            s.Changed += _ => events++;
            s.SetMaxHp(30);
            Assert.AreEqual(0, events);
        }

        // ── Movement ─────────────────────────────────────────────────────────────

        [Test]
        public void MoveToCell_To_Same_Cell_Raises_No_Event()
        {
            var s = new NpcState(Id, 30, 30, 3, 4);
            int events = 0;
            s.Changed += _ => events++;
            s.MoveToCell(3, 4);
            Assert.AreEqual(0, events);
            Assert.AreEqual(3, s.CellX);
            Assert.AreEqual(4, s.CellY);
        }

        [Test]
        public void MoveToCell_And_Hp_Change_Raise_HpChanged_And_Changed()
        {
            var s = new NpcState(Id, 30, 30, 0, 0);
            int hpEvents = 0;
            int changedEvents = 0;
            s.HpChanged += (_, _) => hpEvents++;
            s.Changed += _ => changedEvents++;

            s.MoveToCell(1, 1);
            Assert.AreEqual(1, changedEvents);
            Assert.AreEqual(0, hpEvents);

            s.ApplyDamage(1);
            Assert.AreEqual(2, changedEvents);
            Assert.AreEqual(1, hpEvents);
        }

        // ── Repository ───────────────────────────────────────────────────────────

        [Test]
        public void Repository_GetOrCreate_Is_Idempotent_And_CaseInsensitive()
        {
            var repo = new NpcStateRepository();

            NpcState first = repo.GetOrCreate("Sword_Guard", 30, 30, 0, 0);
            NpcState again = repo.GetOrCreate("sword_guard", 99, 1, 9, 9);

            Assert.AreSame(first, again);
            Assert.AreEqual(30, again.MaxHp);
        }

        [Test]
        public void Repository_Remove_And_All_Track_Entries()
        {
            var repo = new NpcStateRepository();
            repo.GetOrCreate("a", 10, 10, 0, 0);
            repo.GetOrCreate("b", 10, 10, 0, 0);

            Assert.AreEqual(2, repo.All().Count);
            Assert.IsTrue(repo.Remove("A"));
            Assert.IsFalse(repo.Remove("A"));
            Assert.AreEqual(1, repo.All().Count);
        }

        // ── Service ──────────────────────────────────────────────────────────────

        [Test]
        public void Service_Unknown_Id_Is_A_NoOp()
        {
            var session = new GameSession();

            Assert.AreEqual(0, session.NpcStates.ApplyDamage("ghost", 5));
            Assert.AreEqual(0, session.NpcStates.Heal("ghost", 5));
            session.NpcStates.MoveToCell("ghost", 1, 1); // must not throw
            Assert.IsFalse(session.NpcStates.TryCapture("ghost", out _));
        }

        [Test]
        public void Service_Apply_Snapshot_Restores_And_Clamps_Hp()
        {
            var session = new GameSession();
            session.NpcStates.Register(Id, 30, 30, 0, 0);

            // Snapshot claims more HP than maxHp — restore must clamp.
            session.NpcStates.Apply(new NpcStateSnapshot(Id, 999, 20, 5, 6));

            Assert.IsTrue(session.NpcStates.TryGet(Id, out NpcState state));
            Assert.AreEqual(20, state.MaxHp);
            Assert.AreEqual(20, state.Hp);
            Assert.AreEqual(5, state.CellX);
            Assert.AreEqual(6, state.CellY);
        }
    }
}
