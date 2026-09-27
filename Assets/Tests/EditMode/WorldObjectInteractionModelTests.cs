using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for one-time world-object completion. State is stored in WorldFacts, so the
    /// rules survive a simulated scene reload (new model) and a save/restart (snapshot round trip).
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class WorldObjectInteractionModelTests
    {
        [Test]
        public void Cancel_Before_Completion_Leaves_The_Object_Incomplete()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            Assert.IsFalse(model.IsCompleted("chest"));

            // The player leaves the dialogue early and never reports completion.
            Assert.IsFalse(model.IsCompleted("chest"));
        }

        [Test]
        public void One_Time_Object_Completes_Only_Once()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            Assert.IsTrue(model.TryComplete("chest", oneTimeOnly: true));
            Assert.IsTrue(model.IsCompleted("chest"));
            Assert.IsFalse(model.TryComplete("chest", oneTimeOnly: true));
        }

        [Test]
        public void Repeatable_Object_Completes_Every_Time_And_Is_Never_Marked_Completed()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            Assert.IsTrue(model.TryComplete("sign", oneTimeOnly: false));
            Assert.IsTrue(model.TryComplete("sign", oneTimeOnly: false));
            Assert.IsFalse(model.IsCompleted("sign"));
        }

        [Test]
        public void Blank_Id_Cannot_Complete()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            Assert.IsFalse(model.TryComplete("", oneTimeOnly: true));
            Assert.IsFalse(model.TryComplete(null, oneTimeOnly: true));
        }

        [Test]
        public void Completion_Survives_A_Scene_Reload_New_Model_Same_Facts()
        {
            var facts = new WorldFacts();
            new WorldObjectInteractionModel(facts).TryComplete("chest", oneTimeOnly: true);

            var reloadedModel = new WorldObjectInteractionModel(facts);

            Assert.IsTrue(reloadedModel.IsCompleted("chest"));
            Assert.IsFalse(reloadedModel.TryComplete("chest", oneTimeOnly: true));
        }

        [Test]
        public void Completion_Survives_Save_And_Restart()
        {
            var facts = new WorldFacts();
            new WorldObjectInteractionModel(facts).TryComplete("chest", oneTimeOnly: true);

            Dictionary<string, object> saved = facts.GetSnapshot();

            var restoredFacts = new WorldFacts();
            restoredFacts.LoadSnapshot(saved);
            var restoredModel = new WorldObjectInteractionModel(restoredFacts);

            Assert.IsTrue(restoredModel.IsCompleted("chest"));
            Assert.IsFalse(restoredModel.TryComplete("chest", oneTimeOnly: true));
        }

        [Test]
        public void Completion_Key_Is_Stable_And_Prefixed()
        {
            Assert.AreEqual("worldObject.completed.chest", WorldObjectInteractionModel.CompletionKey("chest"));
        }

        [Test]
        public void Constructor_Requires_Facts()
        {
            Assert.Throws<System.ArgumentNullException>(() => new WorldObjectInteractionModel(null));
        }

        [Test]
        public void CanComplete_Does_Not_Record_Completion()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            // Checking eligibility must not consume the object, so a reward can be delivered first.
            Assert.IsTrue(model.CanComplete("chest", oneTimeOnly: true));
            Assert.IsFalse(model.IsCompleted("chest"));
            Assert.IsTrue(model.CanComplete("chest", oneTimeOnly: true));
        }

        [Test]
        public void CommitCompletion_Records_And_Then_Blocks_Re_Completion()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            Assert.IsTrue(model.CommitCompletion("chest", oneTimeOnly: true));
            Assert.IsTrue(model.IsCompleted("chest"));
            Assert.IsFalse(model.CanComplete("chest", oneTimeOnly: true));
            Assert.IsFalse(model.CommitCompletion("chest", oneTimeOnly: true));
        }

        [Test]
        public void Completion_Can_Be_Deferred_Until_The_Reward_Is_Accounted_For()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            // Simulate a failed delivery: eligibility is checked but completion is not committed.
            Assert.IsTrue(model.CanComplete("chest", oneTimeOnly: true));
            Assert.IsFalse(model.IsCompleted("chest"), "an undelivered reward must not consume the object");

            // A later successful attempt commits.
            Assert.IsTrue(model.CommitCompletion("chest", oneTimeOnly: true));
            Assert.IsTrue(model.IsCompleted("chest"));
        }

        [Test]
        public void Repeatable_Object_Can_And_Commits_Every_Time()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            Assert.IsTrue(model.CanComplete("sign", oneTimeOnly: false));
            Assert.IsTrue(model.CommitCompletion("sign", oneTimeOnly: false));
            Assert.IsTrue(model.CanComplete("sign", oneTimeOnly: false));
            Assert.IsFalse(model.IsCompleted("sign"));
        }

        [Test]
        public void Blank_Id_Is_Neither_Completable_Nor_Committed()
        {
            var facts = new WorldFacts();
            var model = new WorldObjectInteractionModel(facts);

            Assert.IsFalse(model.CanComplete("", oneTimeOnly: true));
            Assert.IsFalse(model.CommitCompletion(null, oneTimeOnly: true));
        }
    }
}
