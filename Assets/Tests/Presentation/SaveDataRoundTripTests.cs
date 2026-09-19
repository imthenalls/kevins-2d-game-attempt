using Game.Core;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Verifies the on-disk save format survives a real JsonUtility round trip. JsonUtility silently
    /// drops things it cannot serialize (dictionaries, properties, some nested types), so this is the
    /// cheap way to catch a save-corrupting change before it ships.
    ///
    /// Unity setup: none. Runs in EditMode with no scene.
    /// </summary>
    public class SaveDataRoundTripTests
    {
        [Test]
        public void Populated_SaveData_Survives_JsonUtility_RoundTrip()
        {
            SaveData original = BuildPopulated();

            string json = JsonUtility.ToJson(original, prettyPrint: true);
            SaveData loaded = JsonUtility.FromJson<SaveData>(json);

            AssertSaveDataEqual(original, loaded);
        }

        [Test]
        public void Empty_SaveData_RoundTrips_With_Usable_Lists()
        {
            SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(new SaveData()));

            Assert.AreEqual("WorldA", loaded.activeWorld);
            Assert.IsNotNull(loaded.wallet);
            Assert.IsNotNull(loaded.npcStates);
            Assert.IsNotNull(loaded.inventorySlots);
            Assert.IsNotNull(loaded.worldPositions);
            Assert.AreEqual(0, loaded.npcStates.Count);
        }

        private static SaveData BuildPopulated()
        {
            var data = new SaveData
            {
                saveVersion = 6,
                currentScene = "WorldB",
                playerX = 12.5f,
                playerY = -3.25f,
                activeWorld = "WorldB",
                playerHp = 42,
                playerMp = 17,
                playerMaxHp = 100,
                playerMaxMp = 50,
            };

            data.worldPositions.Add(new WorldPositionSaveEntry { world = "WorldA", scene = "Overworld", x = 1f, y = 2f, z = 0f });
            data.worldPositions.Add(new WorldPositionSaveEntry { world = "WorldB", scene = "WorldB", x = 4f, y = 5f, z = 0f });
            data.worldAbilities.Add(new WorldAbilitySaveEntry { world = "WorldA", abilityId = "dash" });
            data.worldAbilities.Add(new WorldAbilitySaveEntry { world = "WorldB", abilityId = "blink" });

            data.wallet = new WalletSaveData { balance = 33, capacity = 50 };
            data.wallet.transactions.Add(new WalletTransaction
            {
                transactionId = "t1",
                utcTimestamp = "2026-01-01T00:00:00.0000000Z",
                type = WalletTransactionType.Credit,
                amount = 33,
                balanceAfter = 33,
                reason = "test",
                referenceId = "ref",
            });

            data.marketTransactions.Add(new MarketTransaction
            {
                tradeId = "trade1",
                utcTimestamp = "2026-01-01T00:00:00.0000000Z",
                buyerParticipantId = "player",
                sellerParticipantId = "npc",
                itemId = "apple",
                quantity = 2,
                unitPrice = 5,
                totalPrice = 10,
                marketId = "market",
                reason = "Trade apple",
            });

            data.worldFacts.Add(new FactEntry { key = "gate_open", value = "True", type = "bool" });
            data.worldFacts.Add(new FactEntry { key = "kills", value = "7", type = "int" });

            data.activeQuests.Add(new QuestSaveEntry
            {
                questId = "q1",
                activeNodeIds = new List<string> { "start" },
                objectiveCounts = new List<ObjectiveCountEntry>
                {
                    new ObjectiveCountEntry { objectiveId = "obj1", count = 3 },
                },
            });

            data.inventorySlots.Add(new InventorySlotEntry { slotIndex = 0, itemId = "apple", quantity = 5 });
            data.worldAInventorySlots.Add(new InventorySlotEntry { slotIndex = 1, itemId = "sword", quantity = 1 });
            data.worldBInventorySlots.Add(new InventorySlotEntry { slotIndex = 2, itemId = "blink", quantity = 1 });
            data.playerKeys.Add(new KeyringSaveEntry { itemId = "golden_key", quantity = 1 });
            data.playerEquipment.Add(new EquipmentSaveEntry { slot = "Weapon", itemId = "iron_sword" });

            data.npcStates.Add(new NpcSaveEntry
            {
                npcId = "sword_guard",
                x = 1.5f,
                y = 2.5f,
                hasStats = true,
                hp = 20,
                mp = 5,
                maxHp = 30,
                maxMp = 10,
                wallet = new WalletSaveData { balance = 7, capacity = 40 },
                hasModelState = true,
                cellX = 3,
                cellY = 4,
                inventorySlots = new List<InventorySlotEntry>
                {
                    new InventorySlotEntry { slotIndex = 0, itemId = "apple", quantity = 2 },
                },
            });

            data.hotbarSlots.Add(new HotbarEntry { slotIndex = 0, itemId = "apple" });

            return data;
        }

        private static void AssertSaveDataEqual(SaveData a, SaveData b)
        {
            Assert.AreEqual(a.saveVersion, b.saveVersion);
            Assert.AreEqual(a.currentScene, b.currentScene);
            Assert.AreEqual(a.playerX, b.playerX);
            Assert.AreEqual(a.playerY, b.playerY);
            Assert.AreEqual(a.activeWorld, b.activeWorld);
            Assert.AreEqual(a.playerHp, b.playerHp);
            Assert.AreEqual(a.playerMp, b.playerMp);
            Assert.AreEqual(a.playerMaxHp, b.playerMaxHp);
            Assert.AreEqual(a.playerMaxMp, b.playerMaxMp);

            Assert.AreEqual(a.worldPositions.Count, b.worldPositions.Count);
            Assert.AreEqual(a.worldPositions[1].scene, b.worldPositions[1].scene);
            Assert.AreEqual(a.worldAbilities[1].abilityId, b.worldAbilities[1].abilityId);

            Assert.AreEqual(a.wallet.balance, b.wallet.balance);
            Assert.AreEqual(a.wallet.capacity, b.wallet.capacity);
            Assert.AreEqual(a.wallet.transactions.Count, b.wallet.transactions.Count);
            Assert.AreEqual(a.wallet.transactions[0].transactionId, b.wallet.transactions[0].transactionId);
            Assert.AreEqual(a.wallet.transactions[0].type, b.wallet.transactions[0].type);

            Assert.AreEqual(a.marketTransactions.Count, b.marketTransactions.Count);
            Assert.AreEqual(a.marketTransactions[0].totalPrice, b.marketTransactions[0].totalPrice);

            Assert.AreEqual(a.worldFacts.Count, b.worldFacts.Count);
            Assert.AreEqual(a.worldFacts[1].value, b.worldFacts[1].value);

            Assert.AreEqual(a.activeQuests.Count, b.activeQuests.Count);
            Assert.AreEqual(a.activeQuests[0].questId, b.activeQuests[0].questId);
            Assert.AreEqual(a.activeQuests[0].activeNodeIds[0], b.activeQuests[0].activeNodeIds[0]);
            Assert.AreEqual(a.activeQuests[0].objectiveCounts[0].count, b.activeQuests[0].objectiveCounts[0].count);

            Assert.AreEqual(a.inventorySlots[0].itemId, b.inventorySlots[0].itemId);
            Assert.AreEqual(a.inventorySlots[0].quantity, b.inventorySlots[0].quantity);
            Assert.AreEqual(a.worldAInventorySlots[0].itemId, b.worldAInventorySlots[0].itemId);
            Assert.AreEqual(a.worldBInventorySlots[0].itemId, b.worldBInventorySlots[0].itemId);
            Assert.AreEqual(a.playerKeys[0].itemId, b.playerKeys[0].itemId);
            Assert.AreEqual(a.playerEquipment[0].slot, b.playerEquipment[0].slot);

            Assert.AreEqual(a.npcStates.Count, b.npcStates.Count);
            Assert.AreEqual(a.npcStates[0].npcId, b.npcStates[0].npcId);
            Assert.AreEqual(a.npcStates[0].hp, b.npcStates[0].hp);
            Assert.AreEqual(a.npcStates[0].hasModelState, b.npcStates[0].hasModelState);
            Assert.AreEqual(a.npcStates[0].cellX, b.npcStates[0].cellX);
            Assert.AreEqual(a.npcStates[0].wallet.balance, b.npcStates[0].wallet.balance);
            Assert.AreEqual(a.npcStates[0].inventorySlots[0].itemId, b.npcStates[0].inventorySlots[0].itemId);

            Assert.AreEqual(a.hotbarSlots[0].itemId, b.hotbarSlots[0].itemId);
        }
    }
}
