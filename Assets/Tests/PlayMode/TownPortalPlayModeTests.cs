using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies the World A (Overworld) ↔ 3D Town portal is wired in both directions, and that a
    /// player actually travels 2D → 3D → 2D with the correct avatar at each stop.
    /// </summary>
    public class TownPortalPlayModeTests : PlayModeTestBase
    {
        private const string OverworldScene = "Assets/Scenes/Overworld.unity";
        private const string TownScene = "Assets/Scenes/Town.unity";

        [UnityTest]
        public IEnumerator Overworld_Town_Portal_Is_Wired_Both_Ways()
        {
            yield return LoadScene(OverworldScene);

            PortalManager manager = PortalManager.Instance;
            Assert.IsNotNull(manager, "GameBootstrap should provide a PortalManager");

            Assert.IsTrue(manager.TryFindPortal("overworld_town", out IPortalRoute toTown), "the Overworld Town portal must exist");
            Assert.AreEqual("Town", toTown.DestinationScene);
            Assert.AreEqual("town_exit", toTown.DestinationPortalId);

            yield return LoadScene(TownScene);

            Assert.IsTrue(manager.TryFindPortal("town_exit", out IPortalRoute toOverworld), "the Town exit portal must exist");
            Assert.AreEqual("Overworld", toOverworld.DestinationScene);
            Assert.AreEqual("overworld_town", toOverworld.DestinationPortalId);
        }

        [UnityTest]
        public IEnumerator Portal_RoundTrip_2D_To_3D_And_Back()
        {
            yield return LoadScene(OverworldScene);

            PortalManager manager = PortalManager.Instance;
            Assert.IsNotNull(manager);

            PlayerController2D player2D = Object.FindAnyObjectByType<PlayerController2D>();
            Assert.IsNotNull(player2D, "Overworld must have a 2D player");

            Assert.IsTrue(manager.TryFindPortal("overworld_town", out IPortalRoute toTown));
            Assert.IsTrue(manager.TryUsePortal(toTown, player2D.transform), "travel to Town should start");

            yield return WaitForScene("Town");
            PlayerController3D player3D = Object.FindAnyObjectByType<PlayerController3D>();
            Assert.IsNotNull(player3D, "Town must have a 3D player after traveling");
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController2D>(), "no 2D player should remain in Town");

            yield return WaitForCooldown();

            Assert.IsTrue(manager.TryFindPortal("town_exit", out IPortalRoute toOverworld));
            Assert.IsTrue(manager.TryUsePortal(toOverworld, player3D.transform), "travel back to Overworld should start");

            yield return WaitForScene("Overworld");
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController2D>(), "Overworld must have a 2D player after returning");
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController3D>(), "no 3D player should remain in Overworld");
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            for (int i = 0; i < 100; i++)
            {
                if (SceneManager.GetActiveScene().name == sceneName)
                    yield break;
                yield return new WaitForSeconds(0.1f);
            }
            Assert.Fail("Scene '" + sceneName + "' did not load in time.");
        }

        private static IEnumerator WaitForCooldown()
        {
            yield return new WaitForSeconds(0.5f);
            yield return null;
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
