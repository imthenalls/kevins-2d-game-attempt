using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the equipment model. It now lives in Game.Data and depends only on the
    /// IItem contract, so slot rules are covered without a scene.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class EquipmentModelTests
    {
        private sealed class FakeItem : IItem
        {
            public string ItemId { get; }
            public string ItemName => ItemId;
            public ItemType Type { get; }
            public ItemFlags Flags => ItemFlags.None;
            public ItemScope Scope => ItemScope.Shared;
            public int MaxStackSize => 1;
            public bool IsStackable => false;
            public bool IsEquip => Type == ItemType.Equipment;
            public EquipSlotType EquipSlot { get; }

            public FakeItem(string id, EquipSlotType slot = EquipSlotType.Weapon, ItemType type = ItemType.Equipment)
            {
                ItemId = id;
                EquipSlot = slot;
                Type = type;
            }
        }

        [Test]
        public void Equip_Stores_The_Item_And_Reports_Empty_Previous()
        {
            var model = new EquipmentModel();
            var sword = new FakeItem("sword");

            IItem displaced = model.Equip(EquipSlotType.Weapon, sword);

            Assert.IsNull(displaced);
            Assert.AreSame(sword, model.GetEquipped(EquipSlotType.Weapon));
            Assert.IsFalse(model.IsSlotEmpty(EquipSlotType.Weapon));
        }

        [Test]
        public void Equip_Rejects_Wrong_Slot_And_Non_Equipment()
        {
            var model = new EquipmentModel();

            Assert.IsNull(model.Equip(EquipSlotType.Weapon, new FakeItem("boots", EquipSlotType.Armor)));
            Assert.IsNull(model.Equip(EquipSlotType.Weapon, new FakeItem("potion", EquipSlotType.Weapon, ItemType.Consumable)));
            Assert.IsTrue(model.IsSlotEmpty(EquipSlotType.Weapon));
        }

        [Test]
        public void Equip_Swaps_And_Returns_The_Displaced_Item()
        {
            var model = new EquipmentModel();
            var heavy = new FakeItem("heavy");
            var light = new FakeItem("light");
            model.Equip(EquipSlotType.Weapon, heavy);

            IItem displaced = model.Equip(EquipSlotType.Weapon, light);

            Assert.AreSame(heavy, displaced);
            Assert.AreSame(light, model.GetEquipped(EquipSlotType.Weapon));
        }

        [Test]
        public void Unequip_Removes_And_Returns_The_Item()
        {
            var model = new EquipmentModel();
            var sword = new FakeItem("sword");
            model.Equip(EquipSlotType.Weapon, sword);

            IItem removed = model.Unequip(EquipSlotType.Weapon);

            Assert.AreSame(sword, removed);
            Assert.IsTrue(model.IsSlotEmpty(EquipSlotType.Weapon));
            Assert.IsNull(model.Unequip(EquipSlotType.Weapon));
        }

        [Test]
        public void Slot_Changes_Raise_OnSlotChanged_With_New_And_Old()
        {
            var model = new EquipmentModel();
            var heavy = new FakeItem("heavy");
            var light = new FakeItem("light");

            EquipSlotType seenSlot = EquipSlotType.Accessory;
            IItem seenNew = null;
            IItem seenOld = null;
            int changes = 0;
            model.OnSlotChanged += (slot, newItem, oldItem) =>
            {
                changes++;
                seenSlot = slot;
                seenNew = newItem;
                seenOld = oldItem;
            };

            model.Equip(EquipSlotType.Weapon, heavy);
            model.Equip(EquipSlotType.Weapon, light);

            Assert.AreEqual(2, changes);
            Assert.AreEqual(EquipSlotType.Weapon, seenSlot);
            Assert.AreSame(light, seenNew);
            Assert.AreSame(heavy, seenOld);
        }
    }
}
