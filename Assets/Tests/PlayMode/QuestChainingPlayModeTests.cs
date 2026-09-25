using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies a quest can start a follow-up quest during event/automatic-transition processing
    /// without corrupting the active-quest list. Exercises the real bandit "expose" ending, which
    /// starts sheriffs_gratitude, and confirms it starts exactly once.
    /// </summary>
    public class QuestChainingPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Followup_Quest_Starts_Exactly_Once_During_Event()
        {
            QuestManager manager = EnsureQuestManager();
            EnsureWorldStateManager();
            yield return null;

            manager.LoadSaveData(new List<Game.Core.QuestSaveEntry>());
            WorldStateManager.Instance.SetFact("sheriffTrusted", "True");

            manager.StartQuest("bandit_king");
            yield return null;

            QuestEventBus.Raise("NpcTalkedTo", "sheriff", 1);     // start -> investigation
            QuestEventBus.Raise("ItemCollected", "evidence_letter", 1); // investigation -> ending_expose
            yield return null;

            var save = manager.GetSaveData();
            Assert.AreEqual(1, SaveCount(save, "sheriffs_gratitude"),
                "the expose ending must start sheriffs_gratitude exactly once");
            Assert.IsTrue(manager.IsQuestActive("bandit_king"), "the originating quest stays active");
            Assert.IsTrue(WorldStateManager.Instance.HasFact("banditKingExposed"));
        }

        [UnityTest]
        public IEnumerator Duplicate_And_Circular_Start_Requests_Do_Not_Duplicate()
        {
            QuestManager manager = EnsureQuestManager();
            EnsureWorldStateManager();
            yield return null;

            manager.LoadSaveData(new List<Game.Core.QuestSaveEntry>());

            // Start the same quest twice while it is already active (a duplicate request).
            manager.StartQuest("bandit_king");
            manager.StartQuest("bandit_king");
            yield return null;

            Assert.AreEqual(1, SaveCount(manager.GetSaveData(), "bandit_king"),
                "duplicate start requests must not create a second instance");

            // The expose ending also starts the follow-up; a repeated event must not duplicate it.
            WorldStateManager.Instance.SetFact("sheriffTrusted", "True");
            QuestEventBus.Raise("NpcTalkedTo", "sheriff", 1);
            QuestEventBus.Raise("ItemCollected", "evidence_letter", 1);
            QuestEventBus.Raise("ItemCollected", "evidence_letter", 1);
            yield return null;

            Assert.AreEqual(1, SaveCount(manager.GetSaveData(), "sheriffs_gratitude"),
                "repeated completion events must not start the follow-up twice");
        }

        private static int SaveCount(List<Game.Core.QuestSaveEntry> entries, string questId)
        {
            int count = 0;
            foreach (var entry in entries)
                if (entry.questId == questId)
                    count++;
            return count;
        }

        private static QuestManager EnsureQuestManager()
        {
            if (QuestManager.Instance != null)
                return QuestManager.Instance;
            return new GameObject("Quest Manager").AddComponent<QuestManager>();
        }

        private static WorldStateManager EnsureWorldStateManager()
        {
            if (WorldStateManager.Instance != null)
                return WorldStateManager.Instance;
            return new GameObject("World State Manager").AddComponent<WorldStateManager>();
        }
    }
}
