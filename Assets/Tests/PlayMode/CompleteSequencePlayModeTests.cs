using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// End-to-end check of the playable sequence: completing a quest chain grants rewards, mutates
    /// world facts, and that state survives a real save-to-disk round trip (quests, facts, and any
    /// undelivered rewards are all present in the deserialized save). Uses the persistent systems
    /// created by GameBootstrap at play start, so no scene load is required (and no scene state
    /// leaks into sibling tests).
    ///
    /// The test backs up and restores the real save file so it never touches the player's save.
    /// </summary>
    public class CompleteSequencePlayModeTests : PlayModeTestBase
    {
        private string savePath;
        private string backupPath;
        private string tempPath;
        private bool hadSave;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savePath = Path.Combine(Application.persistentDataPath, "save.json");
            backupPath = savePath + ".bak";
            tempPath = savePath + ".tmp";
            hadSave = File.Exists(savePath);

            if (hadSave)
            {
                File.Copy(savePath, savePath + ".testbak", overwrite: true);
                File.Delete(savePath);
            }
            if (File.Exists(backupPath)) File.Delete(backupPath);
            if (File.Exists(tempPath)) File.Delete(tempPath);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (hadSave && File.Exists(savePath + ".testbak"))
            {
                File.Copy(savePath + ".testbak", savePath, overwrite: true);
                File.Delete(savePath + ".testbak");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Quest_Completion_Rewards_And_World_Changes_Survive_Save()
        {
            QuestManager manager = QuestManager.Instance;
            WorldStateManager state = WorldStateManager.Instance;
            SaveManager saveManager = SaveManager.Instance;
            Assert.IsNotNull(manager, "GameBootstrap should provide a QuestManager");
            Assert.IsNotNull(state, "GameBootstrap should provide a WorldStateManager");
            Assert.IsNotNull(saveManager, "GameBootstrap should provide a SaveManager");

            // Reset any pre-existing quest state, then drive the bandit chain to the expose ending.
            manager.LoadSaveData(new List<QuestSaveEntry>());
            state.SetFact("sheriffTrusted", "True");
            manager.StartQuest("bandit_king");
            QuestEventBus.Raise("NpcTalkedTo", "sheriff", 1);          // start -> investigation
            QuestEventBus.Raise("ItemCollected", "evidence_letter", 1); // investigation -> ending_expose
            yield return null;

            Assert.IsTrue(manager.IsQuestActive("sheriffs_gratitude"), "the expose ending starts the follow-up");
            Assert.IsTrue(state.HasFact("banditKingExposed"), "the expose ending writes the world fact");

            // Drive the follow-up to its reward node.
            QuestEventBus.Raise("NpcTalkedTo", "sheriff", 1);          // sheriffs_gratitude -> rewarded
            yield return null;
            Assert.IsTrue(state.HasFact("sheriffsGratitudeComplete"), "the follow-up writes its completion fact");

            saveManager.Save();
            Assert.IsTrue(File.Exists(savePath), "Save must write the save file");

            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
            Assert.AreEqual(8, data.saveVersion);

            Assert.IsTrue(data.activeQuests.Exists(q => q.questId == "bandit_king"), "bandit_king must persist");
            Assert.IsTrue(data.activeQuests.Exists(q => q.questId == "sheriffs_gratitude"), "the follow-up must persist");
            Assert.IsTrue(data.worldFacts.Exists(f => f.key == "banditKingExposed" && f.value == "True"));
            Assert.IsTrue(data.worldFacts.Exists(f => f.key == "sheriffsGratitudeComplete" && f.value == "True"));

            // The follow-up reward (2x health_potion) is either in the inventory or pending; both
            // must be reflected somewhere in the save rather than silently dropped.
            bool rewardPresent = HasItemInInventory(data, "health_potion")
                || data.pendingRewards.Exists(p => p.itemId == "health_potion");
            Assert.IsTrue(rewardPresent, "the follow-up reward must be present (inventory or pending)");

            yield return null;
        }

        private static bool HasItemInInventory(SaveData data, string itemId)
        {
            foreach (var slot in data.worldAInventorySlots)
                if (slot.itemId == itemId) return true;
            foreach (var slot in data.worldBInventorySlots)
                if (slot.itemId == itemId) return true;
            foreach (var slot in data.inventorySlots)
                if (slot.itemId == itemId) return true;
            return false;
        }
    }
}
