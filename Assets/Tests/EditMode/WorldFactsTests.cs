using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the authoritative world-state facts model that the Unity
    /// WorldStateManager facade forwards to.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class WorldFactsTests
    {
        [Test]
        public void Set_Get_Has_And_Clear()
        {
            var facts = new WorldFacts();

            facts.SetFact("gate", true);
            Assert.IsTrue(facts.HasFact("gate"));
            Assert.AreEqual(true, facts.GetFact("gate"));

            facts.ClearFact("gate");
            Assert.IsFalse(facts.HasFact("gate"));
            Assert.IsNull(facts.GetFact("gate"));
        }

        [Test]
        public void Flag_Semantics_Accept_The_String_True_From_Quest_Actions()
        {
            var facts = new WorldFacts();

            facts.SetFact("sheriffTrusted", "True");
            Assert.IsTrue(facts.HasFlag("sheriffTrusted"));

            facts.SetFact("sheriffTrusted", "False");
            Assert.IsFalse(facts.HasFlag("sheriffTrusted"));

            facts.SetFact("sheriffTrusted", false);
            Assert.IsFalse(facts.HasFlag("sheriffTrusted"));

            facts.SetFlag("sheriffTrusted");
            Assert.IsTrue(facts.HasFlag("sheriffTrusted"));
        }

        [Test]
        public void Toggle_Flag_Flips_Presence()
        {
            var facts = new WorldFacts();

            facts.ToggleFlag("bridge");
            Assert.IsTrue(facts.HasFlag("bridge"));

            facts.ToggleFlag("bridge");
            Assert.IsFalse(facts.HasFlag("bridge"));
        }

        [Test]
        public void Typed_Accessors_Read_Stored_Types_And_Strings()
        {
            var facts = new WorldFacts();
            facts.SetInt("kills", 7);
            facts.SetFloat("reputation", 1.5f);
            facts.SetString("hero", "kevin");
            facts.SetFact("level_text", "12");

            Assert.AreEqual(7, facts.GetInt("kills"));
            Assert.AreEqual(1.5f, facts.GetFloat("reputation"), 0.0001f);
            Assert.AreEqual("kevin", facts.GetString("hero"));
            Assert.AreEqual(12, facts.GetInt("level_text"));
            Assert.AreEqual(0, facts.GetInt("missing"));
            Assert.AreEqual("", facts.GetString("missing"));
        }

        [Test]
        public void Snapshot_Round_Trip_Restores_Facts()
        {
            var facts = new WorldFacts();
            facts.SetFlag("banditKingDead");
            facts.SetInt("bounty", 3);
            Dictionary<string, object> snapshot = facts.GetSnapshot();

            facts.ClearFact("banditKingDead");
            facts.SetInt("bounty", 0);
            facts.LoadSnapshot(snapshot);

            Assert.IsTrue(facts.HasFlag("banditKingDead"));
            Assert.AreEqual(3, facts.GetInt("bounty"));
        }

        [Test]
        public void Changed_Event_Fires_On_Set_And_Clear_But_Not_During_Restore()
        {
            var facts = new WorldFacts();
            int changes = 0;
            facts.Changed += _ => changes++;

            facts.SetFact("a", 1);
            facts.ClearFact("a");
            Assert.AreEqual(2, changes);

            var snapshot = new Dictionary<string, object> { { "b", 2 } };
            facts.LoadSnapshot(snapshot);
            Assert.AreEqual(2, changes, "bulk restore must not raise change events");
        }
    }
}
