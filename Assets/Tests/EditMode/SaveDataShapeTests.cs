using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free checks on the save DTO's shape. SaveData now lives in Game.Data, so its defaults
    /// and collection initialization can be verified without Unity or JsonUtility (the JsonUtility
    /// round trip stays in Tests.Presentation).
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class SaveDataShapeTests
    {
        [Test]
        public void Defaults_Match_A_New_Game()
        {
            var data = new SaveData();

            Assert.AreEqual("", data.currentScene);
            Assert.AreEqual("WorldA", data.activeWorld);
            Assert.IsNotNull(data.wallet);
            Assert.IsTrue(data.playerEquipment != null);
        }

        [Test]
        public void Every_Collection_Is_Initialized()
        {
            var data = new SaveData();

            Assert.IsNotNull(data.worldPositions);
            Assert.IsNotNull(data.worldAbilities);
            Assert.IsNotNull(data.marketTransactions);
            Assert.IsNotNull(data.worldFacts);
            Assert.IsNotNull(data.activeQuests);
            Assert.IsNotNull(data.inventorySlots);
            Assert.IsNotNull(data.worldAInventorySlots);
            Assert.IsNotNull(data.worldBInventorySlots);
            Assert.IsNotNull(data.playerKeys);
            Assert.IsNotNull(data.playerEquipment);
            Assert.IsNotNull(data.npcStates);
            Assert.IsNotNull(data.hotbarSlots);
        }

        [Test]
        public void Save_Entry_Types_Are_Plain_And_Independent()
        {
            var entry = new NpcSaveEntry { npcId = "sword_guard", cellX = 3, cellY = 4 };
            var quest = new QuestSaveEntry { questId = "bandit_king" };
            var transaction = new MarketTransaction { itemId = "apple", quantity = 2, unitPrice = 5, totalPrice = 10 };

            Assert.AreEqual("sword_guard", entry.npcId);
            Assert.AreEqual("bandit_king", quest.questId);
            Assert.AreEqual(10, transaction.totalPrice);
            Assert.AreEqual(transaction.totalPrice, transaction.Clone().totalPrice);
        }
    }
}
