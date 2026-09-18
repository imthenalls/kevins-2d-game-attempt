using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Tests for the hotbar assignment model. The hotbar pins an item type per slot; this covers
    /// assignment, clearing, first-empty lookup, and bounds safety.
    ///
    /// Unity setup: none. Runs in EditMode with no scene.
    /// </summary>
    public class HotbarModelTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void Assign_And_Get_Slot()
        {
            var model = new HotbarModel();
            ItemData apple = MakeItem("apple");

            model.Assign(2, apple);

            Assert.AreSame(apple, model.GetSlot(2));
            Assert.IsNull(model.GetSlot(0));
        }

        [Test]
        public void Clear_Empties_The_Slot()
        {
            var model = new HotbarModel();
            model.Assign(0, MakeItem("apple"));

            model.Clear(0);

            Assert.IsNull(model.GetSlot(0));
        }

        [Test]
        public void FirstEmptySlot_Returns_First_Gap_Then_Negative_When_Full()
        {
            var model = new HotbarModel();
            Assert.AreEqual(0, model.FirstEmptySlot());

            for (int i = 0; i < HotbarModel.SlotCount; i++)
                model.Assign(i, MakeItem("item" + i));

            Assert.AreEqual(-1, model.FirstEmptySlot());
        }

        [Test]
        public void Out_Of_Range_Access_Is_Safe()
        {
            var model = new HotbarModel();

            model.Assign(-1, MakeItem("apple"));
            model.Assign(HotbarModel.SlotCount, MakeItem("apple"));

            Assert.IsNull(model.GetSlot(-1));
            Assert.IsNull(model.GetSlot(HotbarModel.SlotCount));
        }

        private ItemData MakeItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.itemName = id;
            item.maxStackSize = 99;
            created.Add(item);
            return item;
        }
    }
}
