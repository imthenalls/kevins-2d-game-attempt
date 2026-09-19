using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Pins the boundary between the engine-free IItem contract and the Unity ItemData asset. If an
    /// IItem member stops matching its backing field (or the stack/equip semantics drift), these
    /// fail — otherwise the mistake compiles and only appears at runtime.
    ///
    /// Unity setup: none. Runs in EditMode with no scene.
    /// </summary>
    public class ItemDataContractTests
    {
        private sealed class FakeItem : IItem
        {
            public string ItemId => "fake";
            public string ItemName => "fake";
            public ItemType Type => ItemType.Misc;
            public ItemFlags Flags => ItemFlags.None;
            public ItemScope Scope => ItemScope.Shared;
            public int MaxStackSize => 1;
            public bool IsStackable => false;
            public bool IsEquip => false;
            public EquipSlotType EquipSlot => EquipSlotType.Weapon;
        }

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void IItem_Scalars_Mirror_ItemData_Fields()
        {
            ItemData data = MakeItem();
            data.itemId = "golden_key";
            data.itemName = "Golden Key";
            data.type = ItemType.Material;
            data.flags = ItemFlags.KeyItem;
            data.scope = ItemScope.WorldB;
            data.maxStackSize = 7;

            IItem item = data;

            Assert.AreEqual("golden_key", item.ItemId);
            Assert.AreEqual("Golden Key", item.ItemName);
            Assert.AreEqual(ItemType.Material, item.Type);
            Assert.AreEqual(ItemFlags.KeyItem, item.Flags);
            Assert.AreEqual(ItemScope.WorldB, item.Scope);
            Assert.AreEqual(7, item.MaxStackSize);
        }

        [TestCase(ItemFlags.None, 99, true)]
        [TestCase(ItemFlags.None, 1, false)]
        [TestCase(ItemFlags.Unique, 99, false)]
        [TestCase(ItemFlags.QuestItem, 99, false)]
        public void IsStackable_Reflects_Flags_And_MaxStack(ItemFlags flags, int maxStack, bool expected)
        {
            ItemData data = MakeItem();
            data.flags = flags;
            data.maxStackSize = maxStack;

            Assert.AreEqual(expected, ((IItem)data).IsStackable);
        }

        [TestCase(ItemType.Equipment, true)]
        [TestCase(ItemType.Consumable, false)]
        [TestCase(ItemType.Misc, false)]
        public void IsEquip_True_Only_For_Equipment(ItemType type, bool expected)
        {
            ItemData data = MakeItem();
            data.type = type;

            Assert.AreEqual(expected, ((IItem)data).IsEquip);
        }

        [Test]
        public void AsItemData_Is_Identity_For_Assets_And_Null_For_Other_Implementations()
        {
            ItemData data = MakeItem();
            data.itemId = "apple";

            Assert.AreSame(data, ((IItem)data).AsItemData());
            Assert.IsNull(new FakeItem().AsItemData());
        }

        [Test]
        public void IconOf_Returns_The_Icon_Or_Null()
        {
            ItemData data = MakeItem();
            Assert.IsNull(((IItem)data).IconOf());

            var texture = new Texture2D(1, 1);
            created.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            created.Add(sprite);
            data.icon = sprite;

            Assert.AreSame(sprite, ((IItem)data).IconOf());
            Assert.IsNull(new FakeItem().IconOf());
        }

        private ItemData MakeItem()
        {
            var data = ScriptableObject.CreateInstance<ItemData>();
            created.Add(data);
            return data;
        }
    }
}
