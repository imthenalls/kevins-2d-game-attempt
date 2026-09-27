using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for NPC gate knowledge (remember, skip, forget, clear, persistence).</summary>
    public class NpcMemoryModelTests
    {
        [Test]
        public void Remember_Then_HasLockedMemory()
        {
            var model = new NpcMemoryModel();

            Assert.IsTrue(model.RememberLockedGate("gate_a", "key_a"));
            Assert.IsTrue(model.HasLockedMemory("gate_a"));
            Assert.AreEqual(1, model.Count);
        }

        [Test]
        public void Blank_Ids_Are_Ignored()
        {
            var model = new NpcMemoryModel();

            Assert.IsFalse(model.RememberLockedGate("", "key_a"));
            Assert.IsFalse(model.RememberLockedGate("gate_a", ""));
            Assert.AreEqual(0, model.Count);
            Assert.IsFalse(model.HasLockedMemory(""));
        }

        [Test]
        public void ShouldSkip_Is_True_When_Remembered_And_Still_No_Key()
        {
            var model = new NpcMemoryModel();
            model.RememberLockedGate("gate_a", "key_a");

            Assert.IsTrue(model.ShouldSkipGate("gate_a", holdsRequiredKey: false));
            Assert.IsTrue(model.HasLockedMemory("gate_a"));
        }

        [Test]
        public void ShouldSkip_Forgets_Once_The_Key_Is_Held()
        {
            var model = new NpcMemoryModel();
            model.RememberLockedGate("gate_a", "key_a");

            Assert.IsFalse(model.ShouldSkipGate("gate_a", holdsRequiredKey: true));
            Assert.IsFalse(model.HasLockedMemory("gate_a"));
            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void ShouldSkip_Is_False_For_Unknown_Gate()
        {
            var model = new NpcMemoryModel();
            Assert.IsFalse(model.ShouldSkipGate("gate_unknown", holdsRequiredKey: false));
        }

        [Test]
        public void ForgetGate_Removes_Only_That_Gate()
        {
            var model = new NpcMemoryModel();
            model.RememberLockedGate("gate_a", "key_a");
            model.RememberLockedGate("gate_b", "key_b");

            Assert.IsTrue(model.ForgetGate("gate_a"));
            Assert.IsFalse(model.HasLockedMemory("gate_a"));
            Assert.IsTrue(model.HasLockedMemory("gate_b"));
        }

        [Test]
        public void Clear_Removes_Everything()
        {
            var model = new NpcMemoryModel();
            model.RememberLockedGate("gate_a", "key_a");
            model.RememberLockedGate("gate_b", "key_b");

            model.Clear();

            Assert.AreEqual(0, model.Count);
            Assert.IsFalse(model.HasLockedMemory("gate_a"));
            Assert.IsFalse(model.HasLockedMemory("gate_b"));
        }

        [Test]
        public void Capture_And_Restore_RoundTrip()
        {
            var model = new NpcMemoryModel();
            model.RememberLockedGate("gate_a", "key_a");
            model.RememberLockedGate("gate_b", "key_b");

            var captured = new List<NpcGateMemory>();
            model.Capture(captured);
            Assert.AreEqual(2, captured.Count);

            var restored = new NpcMemoryModel();
            restored.Restore(captured);

            Assert.IsTrue(restored.HasLockedMemory("gate_a"));
            Assert.IsTrue(restored.TryGetRequiredKey("gate_b", out string key));
            Assert.AreEqual("key_b", key);
        }

        [Test]
        public void Service_Keeps_Knowledge_Per_Npc()
        {
            var service = new NpcMemoryService(new NpcMemoryRepository());
            service.Remember("npc_1", "gate_a", "key_a");

            Assert.IsTrue(service.HasLocked("npc_1", "gate_a"));
            Assert.IsFalse(service.HasLocked("npc_2", "gate_a"));
        }

        [Test]
        public void Service_Capture_And_Apply_RoundTrip()
        {
            var service = new NpcMemoryService(new NpcMemoryRepository());
            service.Remember("npc_1", "gate_a", "key_a");

            Assert.IsTrue(service.TryCapture("npc_1", out NpcMemorySnapshot snapshot));
            Assert.AreEqual(1, snapshot.Gates.Count);

            var fresh = new NpcMemoryService(new NpcMemoryRepository());
            Assert.IsFalse(fresh.HasLocked("npc_1", "gate_a"));
            fresh.Apply(snapshot);
            Assert.IsTrue(fresh.HasLocked("npc_1", "gate_a"));
        }
    }
}
