using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Pure tests for the engine-free InventoryModel. Because InventoryModel now lives in Game.Data
    /// and depends only on the IItem contract, these run without a scene and are mirrored by
    /// `dotnet test` as well as Unity EditMode.
    ///
    /// Unity setup: none.
    /// </summary>
    public class InventoryModelTests
    {
        private sealed class FakeItem : IItem
        {
            public string ItemId { get; }
            public string ItemName { get; }
            public ItemType Type { get; set; }
            public ItemFlags Flags { get; set; }
            public ItemScope Scope { get; set; }
            public int MaxStackSize { get; set; }

            public bool IsStackable =>
                (Flags & (ItemFlags.Unique | ItemFlags.QuestItem)) == 0 && MaxStackSize > 1;

            public bool IsEquip => Type == ItemType.Equipment;

            public FakeItem(
                string id,
                int maxStack = 99,
                ItemFlags flags = ItemFlags.None,
                ItemScope scope = ItemScope.Shared)
            {
                ItemId = id;
                ItemName = id;
                MaxStackSize = maxStack;
                Flags = flags;
                Scope = scope;
            }
        }

        [Test]
        public void AddItem_Stacks_And_Returns_Leftover()
        {
            var inv = new InventoryModel(1, 1);
            var apple = new FakeItem("apple", maxStack: 5);

            Assert.AreEqual(0, inv.AddItem(apple, 3));
            Assert.AreEqual(3, inv.AddItem(apple, 5));
            Assert.AreEqual(5, inv.CountItem(apple));
            Assert.AreEqual(1, inv.AddItem(apple, 1));
        }

        [Test]
        public void CanAddItem_Reflects_Remaining_Space()
        {
            var inv = new InventoryModel(1, 1);
            var apple = new FakeItem("apple", maxStack: 5);

            Assert.IsTrue(inv.CanAddItem(apple, 5));
            inv.AddItem(apple, 5);

            Assert.IsFalse(inv.CanAddItem(apple, 1));
            Assert.IsFalse(inv.CanAddItem(new FakeItem("pear", maxStack: 1), 1));
        }

        [Test]
        public void Unique_Item_Can_Only_Be_Held_Once()
        {
            var inv = new InventoryModel(2, 2);
            var key = new FakeItem("golden_key", maxStack: 99, flags: ItemFlags.Unique);

            Assert.IsTrue(inv.CanAddItem(key, 1));
            inv.AddItem(key, 1);

            Assert.AreEqual(1, inv.CountItem(key));
            Assert.IsFalse(inv.CanAddItem(key, 1));
            Assert.IsFalse(inv.CanAddItem(key, 2));
        }

        [Test]
        public void RemoveItem_Spans_Multiple_Stacks_And_Rejects_Overdraw()
        {
            var inv = new InventoryModel(2, 1);
            var apple = new FakeItem("apple", maxStack: 5);

            inv.GetSlot(0).Set(apple, 5);
            inv.GetSlot(1).Set(apple, 5);

            Assert.IsTrue(inv.RemoveItem(apple, 7));
            Assert.AreEqual(3, inv.CountItem(apple));
            Assert.IsFalse(inv.RemoveItem(apple, 4));
            Assert.AreEqual(3, inv.CountItem(apple));
        }

        [Test]
        public void MoveSlot_Merges_Compatible_Stacks()
        {
            var inv = new InventoryModel(2, 1);
            var apple = new FakeItem("apple", maxStack: 5);
            inv.GetSlot(0).Set(apple, 3);
            inv.GetSlot(1).Set(apple, 2);

            inv.MoveSlot(0, 1);

            Assert.AreEqual(5, inv.GetSlot(1).quantity);
            Assert.IsTrue(inv.GetSlot(0).IsEmpty);
        }

        [Test]
        public void MoveSlot_Swaps_Different_Items()
        {
            var inv = new InventoryModel(2, 1);
            var apple = new FakeItem("apple");
            var pear = new FakeItem("pear");
            inv.GetSlot(0).Set(apple, 1);
            inv.GetSlot(1).Set(pear, 2);

            inv.MoveSlot(0, 1);

            Assert.AreEqual("pear", inv.GetSlot(0).item.ItemId);
            Assert.AreEqual("apple", inv.GetSlot(1).item.ItemId);
            Assert.AreEqual(2, inv.GetSlot(0).quantity);
        }

        [Test]
        public void SplitStack_Moves_Half_Into_An_Empty_Slot()
        {
            var inv = new InventoryModel(1, 2);
            var apple = new FakeItem("apple", maxStack: 10);
            inv.GetSlot(0).Set(apple, 6);

            Assert.IsTrue(inv.SplitStack(0, 4));
            Assert.AreEqual(2, inv.GetSlot(0).quantity);
            Assert.AreEqual(4, inv.GetSlot(1).quantity);
            Assert.IsFalse(inv.SplitStack(0, 2)); // 2 == remaining, cannot leave empty
        }

        [Test]
        public void Sort_Orders_By_Type_Then_Name()
        {
            var inv = new InventoryModel(3, 1);
            var zeta = new FakeItem("zeta") { Type = ItemType.Material };
            var alpha = new FakeItem("alpha") { Type = ItemType.Consumable };
            var beta = new FakeItem("beta") { Type = ItemType.Material };
            inv.GetSlot(0).Set(zeta, 1);
            inv.GetSlot(1).Set(alpha, 1);
            inv.GetSlot(2).Set(beta, 1);

            inv.Sort();

            Assert.AreEqual("alpha", inv.GetSlot(0).item.ItemId);
            Assert.AreEqual("beta", inv.GetSlot(1).item.ItemId);
            Assert.AreEqual("zeta", inv.GetSlot(2).item.ItemId);
        }

        [Test]
        public void World_Scoped_Inventory_Rejects_Other_World_Items_But_Accepts_Shared()
        {
            var inv = new InventoryModel(1, 2, ItemScope.WorldA);
            var worldBOnly = new FakeItem("blink", scope: ItemScope.WorldB);
            var shared = new FakeItem("apple", scope: ItemScope.Shared);

            Assert.IsFalse(inv.Accepts(worldBOnly));
            Assert.AreEqual(1, inv.AddItem(worldBOnly, 1));
            Assert.AreEqual(0, inv.AddItem(shared, 1));
            Assert.AreEqual(1, inv.CountItem(shared));
        }

        [Test]
        public void Transfer_To_Full_Inventory_Leaves_Source_Untouched()
        {
            var source = new InventoryModel(1, 1);
            var destination = new InventoryModel(1, 1);
            var apple = new FakeItem("apple", maxStack: 10);

            source.AddItem(apple, 5);
            destination.AddItem(new FakeItem("pear", maxStack: 1), 1);

            bool moved = source.TryTransferItemTo(destination, apple, 3, out _, out _);

            Assert.IsFalse(moved);
            Assert.AreEqual(5, source.CountItem(apple));
            Assert.AreEqual(0, destination.CountItem(apple));
        }

        [Test]
        public void Transfer_Moves_Items_Without_Loss()
        {
            var source = new InventoryModel(1, 1);
            var destination = new InventoryModel(1, 1);
            var apple = new FakeItem("apple", maxStack: 10);

            source.AddItem(apple, 5);

            Assert.IsTrue(source.TryTransferItemTo(destination, apple, 3, out _, out _));
            Assert.AreEqual(2, source.CountItem(apple));
            Assert.AreEqual(3, destination.CountItem(apple));
        }

        [Test]
        public void AddItem_Raises_OnChanged()
        {
            var inv = new InventoryModel(1, 1);
            int notifications = 0;
            inv.OnChanged += () => notifications++;

            inv.AddItem(new FakeItem("apple"), 1);

            Assert.AreEqual(1, notifications);
        }
    }
}
