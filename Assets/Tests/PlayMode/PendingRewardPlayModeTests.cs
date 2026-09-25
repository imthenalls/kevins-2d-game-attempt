using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies quest rewards that cannot be fully delivered are retained as pending and survive a
    /// save/load round trip. Granting far more than any inventory can hold guarantees a remainder,
    /// making the tests deterministic regardless of the ambient inventory.
    /// </summary>
    public class PendingRewardPlayModeTests : PlayModeTestBase
    {
        private const int OverwhelmQuantity = 10000;
        private PendingRewardManager manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            manager = EnsureManager();
            manager.LoadSaveData(new List<Game.Core.PendingRewardEntry>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Undeliverable_Reward_Is_Retained_As_Pending()
        {
            manager.GrantReward("test_quest", "health_potion", OverwhelmQuantity);

            Assert.IsTrue(manager.HasPendingRewards, "the undeliverable remainder must be retained");
            Assert.AreEqual(1, manager.Pending.Count);
            Assert.AreEqual("test_quest", manager.Pending[0].questId);
            Assert.AreEqual("health_potion", manager.Pending[0].itemId);
            Assert.Greater(manager.Pending[0].remaining, 0, "some quantity must remain pending");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Pending_Rewards_Survive_Save_Load_RoundTrip()
        {
            manager.GrantReward("test_quest", "health_potion", OverwhelmQuantity);
            int remaining = manager.Pending[0].remaining;

            List<Game.Core.PendingRewardEntry> saved = manager.GetSaveData();
            manager.LoadSaveData(new List<Game.Core.PendingRewardEntry>()); // simulate a fresh load
            manager.LoadSaveData(saved);

            Assert.IsTrue(manager.HasPendingRewards);
            Assert.AreEqual(remaining, manager.Pending[0].remaining, "pending quantity must survive a round trip");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadSaveData_Ignores_Invalid_Entries()
        {
            manager.LoadSaveData(new List<Game.Core.PendingRewardEntry>
            {
                new Game.Core.PendingRewardEntry { rewardId = "a", questId = "q", itemId = "health_potion", remaining = 0 },
                new Game.Core.PendingRewardEntry { rewardId = "b", questId = "q", itemId = "", remaining = 5 },
                new Game.Core.PendingRewardEntry { rewardId = "c", questId = "q", itemId = "health_potion", remaining = 2 },
            });

            Assert.AreEqual(1, manager.Pending.Count, "entries with no remaining or no item id must be dropped");
            Assert.AreEqual("health_potion", manager.Pending[0].itemId);
            Assert.AreEqual(2, manager.Pending[0].remaining);
            yield return null;
        }

        private static PendingRewardManager EnsureManager()
        {
            if (PendingRewardManager.Instance != null)
                return PendingRewardManager.Instance;
            return new GameObject("Pending Reward Manager").AddComponent<PendingRewardManager>();
        }
    }
}
