using System.Collections;
using System.IO;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies that every player avatar carries the CharacterStatistics component and that the
    /// cumulative "player" statistics survive travel between scenes and a real save/load. The totals
    /// live in GameSession, which is process-scoped, so the assertions capture the before/after total
    /// rather than assuming a clean bucket.
    ///
    /// Save/load runs against a disposable sandbox directory (see
    /// <see cref="PlayModeSaveFileTestBase"/>), so it never touches the player's real save.
    /// </summary>
    public class CharacterStatisticsPlayModeTests : PlayModeSaveFileTestBase
    {
        private const string OverworldScene = "Assets/Scenes/Overworld.unity";
        private const string WorldBScene = "Assets/Scenes/WorldB.unity";
        private const string TownScene = "Assets/Scenes/Town.unity";

        [UnityTest]
        public IEnumerator Every_Player_Avatar_Has_CharacterStatistics()
        {
            yield return LoadWorld(OverworldScene, WorldLayer.WorldA);
            Assert.IsNotNull(FindPlayerStatistics(), "the World A avatar must expose CharacterStatistics");

            yield return LoadWorld(WorldBScene, WorldLayer.WorldB);
            Assert.IsNotNull(FindPlayerStatistics(), "the World B avatar must expose CharacterStatistics");

            yield return LoadWorld(TownScene, WorldLayer.WorldA);
            Assert.IsNotNull(FindPlayerStatistics(), "the Town avatar must expose CharacterStatistics");
        }

        [UnityTest]
        public IEnumerator Statistics_Recorded_In_One_Scene_Are_Visible_After_Travel()
        {
            yield return LoadWorld(OverworldScene, WorldLayer.WorldA);

            CharacterStatistics stats = FindPlayerStatistics();
            Assert.IsNotNull(stats, "the World A avatar must expose CharacterStatistics");

            stats.RecordItemGathered(2);
            int recorded = GameSessionHost.Session.Statistics.GetOrCreate("player").TotalItemsGathered;

            yield return LoadWorld(TownScene, WorldLayer.WorldA);

            Assert.IsTrue(
                GameSessionHost.Session.Statistics.TryGet("player", out CharacterStatisticsModel model),
                "the shared player statistics bucket must exist after travel");
            Assert.AreEqual(recorded, model.TotalItemsGathered,
                "statistics recorded in one scene must survive the trip");
        }

        [UnityTest]
        public IEnumerator Player_Statistics_Survive_Save_And_Reload()
        {
            yield return LoadWorld(OverworldScene, WorldLayer.WorldA);

            SaveManager saveManager = SaveManager.Instance;
            SceneLoader sceneLoader = SceneLoader.Instance;
            Assert.IsNotNull(saveManager, "GameBootstrap should provide a SaveManager");
            Assert.IsNotNull(sceneLoader, "GameBootstrap should provide a SceneLoader");

            CharacterStatistics stats = FindPlayerStatistics();
            Assert.IsNotNull(stats, "the avatar must expose CharacterStatistics");

            // Record a known amount on top of whatever the shared bucket already holds.
            stats.RecordItemGathered(7);
            int savedTotal = GameSessionHost.Session.Statistics.GetOrCreate("player").TotalItemsGathered;

            saveManager.Save();
            Assert.IsTrue(saveManager.HasSave(), "Save must write the save file");

            // The statistic must be in the serialized JSON, i.e. produced by the real save path.
            SaveData onDisk = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            Assert.AreEqual(SaveData.CurrentVersion, onDisk.saveVersion);
            Assert.AreEqual(savedTotal, onDisk.playerStatistics.totalItemsGathered,
                "the player statistic must be serialized into the save file");

            // Wipe the in-memory totals so a successful load can only come from disk.
            GameSessionHost.Session.Statistics.GetOrCreate("player").Load(new CharacterStatisticsSnapshot());
            Assert.AreEqual(0, GameSessionHost.Session.Statistics.GetOrCreate("player").TotalItemsGathered);

            bool loadComplete = false;
            System.Action onComplete = () => loadComplete = true;
            sceneLoader.OnLoadComplete += onComplete;
            try
            {
                Assert.IsTrue(saveManager.Load(), "SaveManager.Load must accept the save it just wrote");

                for (int i = 0; i < 600 && !loadComplete; i++)
                    yield return null;

                Assert.IsTrue(loadComplete, "the save load must finish");
            }
            finally
            {
                sceneLoader.OnLoadComplete -= onComplete;
            }

            // The recreated avatar's statistics resolve to the shared "player" bucket, now from disk.
            yield return null;
            Assert.IsNotNull(FindPlayerStatistics(), "a player must exist after the reload");
            Assert.AreEqual(savedTotal,
                GameSessionHost.Session.Statistics.GetOrCreate("player").TotalItemsGathered,
                "the saved statistic must be restored after a real save/load");
        }

        [UnityTest]
        public IEnumerator Wallet_Credits_Reach_Statistics_Added_After_The_Wallet()
        {
            GameSessionHost.EnsureExists();

            // Reproduce the real Awake order: the Wallet exists (and caches nothing) before
            // WorldCharacter.Awake adds CharacterStatistics. A cached null would drop every credit.
            var playerObject = new GameObject("player");
            playerObject.tag = "Player";
            try
            {
                Wallet wallet = playerObject.AddComponent<Wallet>();
                wallet.InitializeMana(0, 100);
                playerObject.AddComponent<CharacterStatistics>();

                int before = GameSessionHost.Session.Statistics.GetOrCreate("player").TotalMoneyGained;

                Assert.IsTrue(wallet.Add(5, "wallet-statistics-test"), "the wallet must accept the credit");

                int after = GameSessionHost.Session.Statistics.GetOrCreate("player").TotalMoneyGained;
                Assert.AreEqual(before + 5, after, "a credited amount must reach late-added statistics");
            }
            finally
            {
                Object.Destroy(playerObject);
            }

            yield return null;
        }

        private static CharacterStatistics FindPlayerStatistics()
        {
            PlayerControllerBase player = Object.FindAnyObjectByType<PlayerControllerBase>();
            return player != null ? player.GetComponent<CharacterStatistics>() : null;
        }

        private static IEnumerator LoadWorld(string path, WorldLayer world)
        {
            if (WorldTravelState.Instance != null)
                WorldTravelState.Instance.SetCurrentWorld(world);

            AsyncOperation operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
                yield return null;
            yield return null;
            yield return null;
        }
    }
}
