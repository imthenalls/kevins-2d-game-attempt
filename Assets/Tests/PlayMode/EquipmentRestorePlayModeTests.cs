using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies the save-restore equipment path: re-equipping during a load must not heal current HP
    /// or double-count maximums. The restore mode only re-applies attack/defense and raises the HP
    /// maximum without healing; the wallet/mana are left untouched.
    /// </summary>
    public class EquipmentRestorePlayModeTests : PlayModeTestBase
    {
        private GameObject subject;
        private EntityStats stats;
        private EquipmentManager equipment;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            subject = new GameObject("Equipment Restore Subject");
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
        public IEnumerator Restore_Equip_Raises_Max_Without_Healing()
        {
            ItemData armor = MakeItem("bonus_armor", EquipSlotType.Armor, bonusMaxHp: 20, bonusMaxMp: 10, bonusAttack: 4);

            // Normal gameplay equip: max rises and current HP heals by the bonus.
            equipment.Equip(EquipSlotType.Armor, armor);
            Assert.AreEqual(70, stats.MaxHp);
            Assert.AreEqual(70, stats.Hp, "normal equip heals the added maximum");

            // Player took damage after equipping.
            stats.SetHp(30);

            // Simulate a load: restore base maximum, re-equip without healing, then set current HP.
            equipment.SetRestoring(true);
            equipment.Unequip(EquipSlotType.Armor);
            stats.SetMaxHp(50); // base maximum from the save
            equipment.Equip(EquipSlotType.Armor, armor);
            equipment.SetRestoring(false);
            stats.SetHp(30); // saved current HP

            Assert.AreEqual(70, stats.MaxHp, "restored maximum must equal base + bonus");
            Assert.AreEqual(30, stats.Hp, "restore must not heal the re-applied bonus");
            Assert.AreEqual(4, stats.BonusAttack, "attack bonus still applies on restore");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Normal_Equip_Healing_Is_Unchanged_After_The_Restore_Flag()
        {
            ItemData armor = MakeItem("bonus_armor3", EquipSlotType.Armor, bonusMaxHp: 15);

            stats.SetHp(20);

            // Restore mode does not heal.
            equipment.SetRestoring(true);
            equipment.Equip(EquipSlotType.Armor, armor);
            equipment.SetRestoring(false);
            Assert.AreEqual(65, stats.MaxHp);
            Assert.AreEqual(20, stats.Hp, "restore mode must not heal");

            // Once the flag is off, normal equip heals the added maximum again.
            equipment.Unequip(EquipSlotType.Armor);
            equipment.Equip(EquipSlotType.Armor, armor);
            Assert.AreEqual(65, stats.MaxHp);
            Assert.AreEqual(35, stats.Hp, "normal equip heals the added maximum (20 + 15)");
            yield return null;
        }

        private ItemData MakeItem(string id, EquipSlotType slot, int bonusMaxHp = 0, int bonusMaxMp = 0, int bonusAttack = 0)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.itemName = id;
            item.maxStackSize = 1;
            item.type = ItemType.Equipment;
            item.equipSlot = slot;
            item.bonusMaxHp = bonusMaxHp;
            item.bonusMaxMp = bonusMaxMp;
            item.bonusAttack = bonusAttack;
            return item;
        }
    }
}
