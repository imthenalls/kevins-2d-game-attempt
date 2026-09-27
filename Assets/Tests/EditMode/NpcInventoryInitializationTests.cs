using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the NPC starting-inventory seeding policy. The one-time decision and the
    /// "already initialized" state live in Game.Core, so these run without a scene and are mirrored by
    /// `dotnet test`.
    ///
    /// Unity setup: none.
    /// </summary>
    public class NpcInventoryInitializationTests
    {
        private sealed class FakeItem : IItem
        {
            public string ItemId { get; }
            public string ItemName { get; }
            public ItemType Type { get; set; }
            public ItemFlags Flags { get; set; }
            public ItemScope Scope { get; set; }
            public int MaxStackSize { get; set; } = 99;

            public bool IsStackable => (Flags & (ItemFlags.Unique | ItemFlags.QuestItem)) == 0 && MaxStackSize > 1;
            public bool IsEquip => false;
            public EquipSlotType EquipSlot => EquipSlotType.Weapon;

            public FakeItem(string id) => ItemId = ItemName = id;
        }

        [Test]
        public void TryClaimSeed_Seeds_Once()
        {
            var service = new NpcInventoryInitializationService();
            var inventory = new InventoryModel(2, 2);

            Assert.IsTrue(service.TryClaimSeed("npc_1", inventory));
            Assert.IsTrue(inventory.IsInitialized);
            Assert.IsTrue(service.IsInitialized("npc_1"));

            Assert.IsFalse(service.TryClaimSeed("npc_1", inventory));
        }

        [Test]
        public void TryClaimSeed_Does_Not_Reseed_An_Emptied_Inventory()
        {
            var service = new NpcInventoryInitializationService();
            var inventory = new InventoryModel(2, 2);
            var item = new FakeItem("house_key");

            Assert.IsTrue(service.TryClaimSeed("npc_1", inventory));
            inventory.AddItem(item, 1);
            inventory.RemoveItem(item, 1);
            Assert.IsTrue(inventory.GetSlot(0).IsEmpty);

            Assert.IsFalse(service.TryClaimSeed("npc_1", inventory));
        }

        [Test]
        public void TryClaimSeed_Is_Isolated_Per_Npc()
        {
            var service = new NpcInventoryInitializationService();
            var first = new InventoryModel(1, 1);
            var second = new InventoryModel(1, 1);

            Assert.IsTrue(service.TryClaimSeed("npc_1", first));
            Assert.IsTrue(service.TryClaimSeed("npc_2", second));

            Assert.IsFalse(service.TryClaimSeed("npc_1", first));
            Assert.IsFalse(service.TryClaimSeed("npc_2", second));
        }

        [Test]
        public void TryClaimSeed_Skips_A_Restored_Inventory_And_Records_It()
        {
            var service = new NpcInventoryInitializationService();
            var inventory = new InventoryModel(1, 1);
            inventory.MarkInitialized();

            Assert.IsFalse(service.TryClaimSeed("npc_1", inventory));
            Assert.IsTrue(service.IsInitialized("npc_1"));
        }

        [Test]
        public void Blank_Id_Is_Never_Seeded()
        {
            var service = new NpcInventoryInitializationService();

            Assert.IsFalse(service.TryClaimSeed("", new InventoryModel(1, 1)));
            Assert.IsFalse(service.IsInitialized(""));
        }

        [Test]
        public void Capture_And_Apply_RoundTrip()
        {
            var service = new NpcInventoryInitializationService();
            service.TryClaimSeed("npc_1", new InventoryModel(1, 1));

            Assert.IsTrue(service.TryCapture("npc_1", out NpcInventoryInitializationSnapshot snapshot));
            Assert.IsTrue(snapshot.Initialized);

            var fresh = new NpcInventoryInitializationService();
            Assert.IsFalse(fresh.IsInitialized("npc_1"));
            fresh.Apply(snapshot);
            Assert.IsTrue(fresh.IsInitialized("npc_1"));
        }

        [Test]
        public void TryCapture_Returns_False_For_Unknown_Npc()
        {
            var service = new NpcInventoryInitializationService();
            Assert.IsFalse(service.TryCapture("npc_unknown", out _));
        }

        [Test]
        public void ResetSession_Clears_Initialization()
        {
            var service = new NpcInventoryInitializationService();
            service.TryClaimSeed("npc_1", new InventoryModel(1, 1));

            service.ResetSession();

            Assert.IsFalse(service.IsInitialized("npc_1"));
            Assert.IsTrue(service.TryClaimSeed("npc_1", new InventoryModel(1, 1)));
        }

        [Test]
        public void MarkInitialized_Is_Idempotent_And_Marks_The_Inventory()
        {
            var service = new NpcInventoryInitializationService();
            var inventory = new InventoryModel(1, 1);

            service.MarkInitialized("npc_1", inventory);
            service.MarkInitialized("npc_1", inventory);

            Assert.IsTrue(service.IsInitialized("npc_1"));
            Assert.IsTrue(inventory.IsInitialized);
        }
    }
}
