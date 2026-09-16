using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode integration tests: load the real scenes and assert the runtime composition root
    /// comes up and the NPC adapters register their pure-C# models into the session.
    ///
    /// Complements Tools/verify-smoke.ps1 (which only checks that no errors are logged) by asserting
    /// behaviour. The scenes must be enabled in Build Settings.
    ///
    /// Unity setup: none. Run via the Test Runner (PlayMode) or `unity command run_tests --mode PlayMode`.
    /// </summary>
    public class SceneIntegrationPlayModeTests
    {
        private const string OverworldScene = "Assets/Scenes/Overworld.unity";
        private const string WorldBScene = "Assets/Scenes/WorldB.unity";

        [UnityTest]
        public IEnumerator Overworld_Loads_And_Registers_Npc_Models()
        {
            yield return LoadScene(OverworldScene);

            Assert.IsNotNull(GameSessionHost.Session, "The scene should bootstrap a GameSession.");
            Assert.Greater(
                GameSessionHost.Session.Npcs.All().Count,
                0,
                "NPC adapters (NpcStateView) should register their models on enable.");
        }

        [UnityTest]
        public IEnumerator Session_Survives_A_Scene_Change()
        {
            yield return LoadScene(OverworldScene);
            GameSession session = GameSessionHost.Session;
            Assert.IsNotNull(session);

            yield return LoadScene(WorldBScene);

            Assert.AreSame(session, GameSessionHost.Session, "The session must persist across scene loads.");
            Assert.IsNotNull(GameSessionHost.Session.NpcStates, "The command service must remain usable.");
        }

        private static IEnumerator LoadScene(string path)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
                yield return null;

            // Let Awake / OnEnable / Start run before asserting.
            yield return null;
            yield return null;
        }
    }
}
