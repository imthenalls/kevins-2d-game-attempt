using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode integration tests for equipment: bonuses must be applied to EntityStats when a
    /// slot changes, swapped cleanly, and removed on unequip. Runs Awake/Start for real.
    /// </summary>
    public class EquipmentIntegrationPlayModeTests : PlayModeTestBase
    {
        private GameObject subject;
        private EntityStats stats;
        private EquipmentManager equipment;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            subject = new GameObject("Equipment Subject");
            stats = subject.AddComponent<EntityStats>();
            equipment = subject.AddComponent<EquipmentManager>();
            yield return null;
            stats.Configure(50, 10);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (subject != null)
                Object.Destroy(subject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Equipping_Applies_Item_Bonuses()
        {
            int maxHpBefore = stats.MaxHp;
            ItemData weapon = MakeItem("test_weapon", EquipSlotType.Weapon, bonusMaxHp: 10, bonusAttack: 3);

            Assert.IsNull(equipment.Equip(EquipSlotType.Weapon, weapon));

            Assert.AreEqual(maxHpBefore + 10, stats.MaxHp);
            Assert.AreEqual(50 + 10, stats.Hp); // healDelta grants the added maxHp
            Assert.AreEqual(3, stats.BonusAttack);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Swapping_Weapon_Replaces_Bonuses_And_Returns_The_Old_Item()
        {
            int maxHpBefore = stats.MaxHp;
            ItemData heavy = MakeItem("heavy_blade", EquipSlotType.Weapon, bonusMaxHp: 10, bonusAttack: 3);
            ItemData light = MakeItem("light_blade", EquipSlotType.Weapon, bonusMaxHp: 5, bonusAttack: 2);
            equipment.Equip(EquipSlotType.Weapon, heavy);

            ItemData displaced = equipment.Equip(EquipSlotType.Weapon, light);

            Assert.AreSame(heavy, displaced);
            Assert.AreEqual(maxHpBefore + 5, stats.MaxHp);
            Assert.AreEqual(2, stats.BonusAttack);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unequipping_Removes_The_Bonus()
        {
            int maxHpBefore = stats.MaxHp;
            ItemData weapon = MakeItem("test_weapon", EquipSlotType.Weapon, bonusMaxHp: 10, bonusAttack: 3);
            equipment.Equip(EquipSlotType.Weapon, weapon);

            ItemData removed = equipment.Unequip(EquipSlotType.Weapon);

            Assert.AreSame(weapon, removed);
            Assert.AreEqual(maxHpBefore, stats.MaxHp);
            Assert.AreEqual(0, stats.BonusAttack);
            Assert.LessOrEqual(stats.Hp, stats.MaxHp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Only_Equipment_Of_The_Matching_Slot_Is_Accepted()
        {
            ItemData potion = MakeItem("test_potion", type: ItemType.Consumable);

            Assert.IsNull(equipment.Equip(EquipSlotType.Weapon, potion));
            Assert.IsNull(equipment.Equip(EquipSlotType.Weapon, MakeItem("wrong_slot", equipSlot: EquipSlotType.Armor)));
            Assert.IsTrue(equipment.Model.IsSlotEmpty(EquipSlotType.Weapon));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TryEquipFromInventory_Moves_The_Item_And_Refunds_Displaced_Ones()
        {
            ItemData weapon = MakeItem("test_weapon", EquipSlotType.Weapon, bonusAttack: 2);
            var inventory = new InventoryModel(2, 2);
            inventory.AddItem(weapon, 1);

            ItemData previous = MakeItem("old_blade", EquipSlotType.Weapon);
            equipment.Equip(EquipSlotType.Weapon, previous);

            Assert.IsTrue(equipment.TryEquipFromInventory(inventory, weapon, out ItemData displaced));

            Assert.AreSame(previous, displaced);
            Assert.AreSame(previous, inventory.GetSlot(0).item);
            Assert.AreEqual(0, inventory.CountItem(weapon));
            Assert.AreEqual(2, stats.BonusAttack);
            yield return null;
        }

        private ItemData MakeItem(
            string id,
            EquipSlotType equipSlot = EquipSlotType.Accessory,
            ItemType type = ItemType.Equipment,
            int bonusMaxHp = 0,
            int bonusAttack = 0)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.itemName = id;
            item.maxStackSize = 1;
            item.type = type;
            item.equipSlot = equipSlot;
            item.bonusMaxHp = bonusMaxHp;
            item.bonusAttack = bonusAttack;
            return item;
        }
    }
}
