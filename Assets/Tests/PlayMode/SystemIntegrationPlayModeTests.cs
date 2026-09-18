using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode integration tests for the runtime systems: portal destination resolution against a
    /// real scene, and world-state fact storage with a snapshot round trip.
    /// </summary>
    public class SystemIntegrationPlayModeTests
    {
        private const string OverworldScene = "Assets/Scenes/Overworld.unity";

        [UnityTest]
        public IEnumerator Overworld_Resolves_Portal_Destinations()
        {
            yield return LoadScene(OverworldScene);

            Assert.IsNotNull(PortalManager.Instance, "Overworld should provide a PortalManager.");

            Assert.IsTrue(
                PortalManager.Instance.TryFindPortal("world_b_portal", out PortalTrigger2D portal),
                "The hub portal should exist and be findable by id.");
            Assert.AreEqual("WorldB", portal.DestinationScene);
            Assert.IsFalse(PortalManager.Instance.TryFindPortal("no_such_portal_xyz", out _));

            if (PortalManager.Instance != null)
                Object.Destroy(PortalManager.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator World_Facts_RoundTrip_Through_A_Snapshot()
        {
            var go = new GameObject("World State");
            WorldStateManager state = go.AddComponent<WorldStateManager>();
            yield return null;

            state.SetFlag("gate_open");
            state.SetInt("kills", 5);
            state.SetString("hero", "kevin");

            var snapshot = state.GetSnapshot();
            state.ClearFact("gate_open");
            state.SetInt("kills", 0);
            state.SetString("hero", "");

            state.LoadSnapshot(snapshot);

            Assert.IsTrue(state.HasFlag("gate_open"));
            Assert.AreEqual(5, state.GetInt("kills"));
            Assert.AreEqual("kevin", state.GetString("hero"));
            Assert.IsFalse(state.HasFact("unset_fact"));

            Object.Destroy(go);
            yield return null;
        }

        private static IEnumerator LoadScene(string path)
        {
            AsyncOperation operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(path, UnityEngine.SceneManagement.LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
                yield return null;
            yield return null;
            yield return null;
        }
    }
}
