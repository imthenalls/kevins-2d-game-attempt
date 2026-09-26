using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// End-to-end coverage of the "open the main building" quest: the locked door, the key from the
    /// caretaker's correct dialogue line (a manual quest transition), the hidden key pickup, and
    /// quest completion when the door is used.
    /// </summary>
    public class MainBuildingQuestPlayModeTests : PlayModeTestBase
    {
        private const string TownScene = "Assets/Scenes/Town.unity";

        [UnityTest]
        public IEnumerator Main_Building_Quest_Completes_After_Key_And_Door()
        {
            yield return LoadScene(TownScene);

            QuestManager manager = QuestManager.Instance;
            Assert.IsNotNull(manager);
            manager.LoadSaveData(new List<Game.Core.QuestSaveEntry>());
            manager.StartQuest("open_main_building");
            yield return null;
            Assert.IsTrue(manager.IsQuestInNode("open_main_building", "find_key"));

            // The main building door is locked with the special key.
            PortalManager portals = PortalManager.Instance;
            Assert.IsNotNull(portals);
            Assert.IsTrue(portals.TryFindPortal("main_building_door", out IPortalRoute door));
            Assert.AreEqual("main_building_key", door.RequiredKeyId);

            // The caretaker's correct line advances the quest and grants the key.
            Assert.IsTrue(manager.TryChooseTransition("open_main_building", "find_key", "got_key_via_npc"));
            yield return null;
            Assert.IsTrue(PlayerKeyring.Instance.HasKey("main_building_key"), "the key enters the keyring");
            Assert.IsTrue(manager.IsQuestInNode("open_main_building", "open_door"));

            // Using the door (key satisfied) raises PortalUsed and completes the quest.
            PlayerController3D player = Object.FindAnyObjectByType<PlayerController3D>();
            Assert.IsNotNull(player);
            Assert.IsTrue(portals.TryUsePortal(door, player.transform));
            yield return null;
            Assert.IsTrue(WorldStateManager.Instance.HasFact("Quest.OpenMainBuilding.Completed"),
                "using the main building door completes the quest");
        }

        [UnityTest]
        public IEnumerator Quest_Giver_Dialogue_Requires_Being_Home()
        {
            yield return LoadScene(TownScene);

            NpcController giver = FindNpc("town_npc_1");
            Assert.IsNotNull(giver, "the quest giver (town_npc_1) must exist");
            NpcDialogue dialogue = giver.GetComponent<NpcDialogue>();
            Assert.IsNotNull(dialogue);
            Assert.AreEqual("quest_giver", dialogue.DialogueId);

            // The giver starts Away (wandering); the quest dialogue must be unavailable.
            Assert.IsFalse(dialogue.CanStartDialogue(giver.transform.position),
                "the quest dialogue is unavailable while the giver is not home");
        }

        [UnityTest]
        public IEnumerator Hidden_Key_Pickup_Exists_Behind_A_Building()
        {
            yield return LoadScene(TownScene);

            ItemPickup found = null;
            foreach (ItemPickup pickup in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Exclude))
            {
                var field = typeof(ItemPickup).GetField(
                    "itemId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null && (string)field.GetValue(pickup) == "main_building_key")
                {
                    found = pickup;
                    break;
                }
            }

            Assert.IsNotNull(found, "a hidden main_building_key pickup must exist behind a building");
        }

        private static NpcController FindNpc(string npcId)
        {
            foreach (NpcController npc in Object.FindObjectsByType<NpcController>(FindObjectsInactive.Exclude))
                if (npc.NpcId == npcId)
                    return npc;
            return null;
        }

        private static IEnumerator LoadScene(string path)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
                yield return null;
            yield return null;
            yield return null;
        }
    }
}
