using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode checks for school room-based visibility: the occupied zone is revealed, every other
    /// zone's cover stays on, transitions across doorways are stable, the initial spawn and a portal
    /// arrival set visibility from the player's position. Only the active player drives it.
    /// </summary>
    public class SchoolVisibilityPlayModeTests : PlayModeTestBase
    {
        private const string TownScene = "Assets/Scenes/Town.unity";

        private SchoolVisibilityController controller;
        private PlayerController3D player;
        private SchoolZoneCover[] covers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadScene(TownScene);

            controller = Object.FindAnyObjectByType<SchoolVisibilityController>();
            player = Object.FindAnyObjectByType<PlayerController3D>();
            covers = Object.FindObjectsByType<SchoolZoneCover>(FindObjectsInactive.Exclude);

            Assert.IsNotNull(controller, "the school must have a SchoolVisibilityController.");
            Assert.IsNotNull(player, "Town must have a 3D player.");
            Assert.Greater(covers.Length, 20, "expected one cover per school zone.");
        }

        [UnityTest]
        public IEnumerator Initial_Spawn_Outside_The_School_Covers_Everything()
        {
            controller.ForceRefresh();
            yield return null;

            Assert.AreEqual(string.Empty, controller.ActiveZoneId,
                "the Town spawn is outside the school, so no zone is active.");
            AssertOnlyHidden(string.Empty);
        }

        [UnityTest]
        public IEnumerator Hall_Reveals_Hall_And_Covers_Rooms()
        {
            PlacePlayer(150.5f, 7.5f); // entrance lobby cell (50,7)
            controller.ForceRefresh();
            yield return null;

            Assert.AreEqual(SchoolZoneLayout.HallId, controller.ActiveZoneId);
            AssertOnlyHidden(SchoolZoneLayout.HallId);
        }

        [UnityTest]
        public IEnumerator Room_Reveals_Room_And_Covers_Hall_And_Other_Rooms()
        {
            PlacePlayer(180.5f, 40.5f); // gym cell (80,40)
            controller.ForceRefresh();
            yield return null;

            Assert.AreEqual("gym", controller.ActiveZoneId);
            AssertOnlyHidden("gym");
            AssertCoverVisible(SchoolZoneLayout.HallId, true);
            AssertCoverVisible("ms_classroom_1", true);
        }

        [UnityTest]
        public IEnumerator Room_To_Room_Switches_The_Revealed_Zone()
        {
            PlacePlayer(180.5f, 40.5f); // gym
            controller.ForceRefresh();
            yield return null;
            Assert.AreEqual("gym", controller.ActiveZoneId);

            PlacePlayer(190.5f, 30.5f); // boys' locker room (90,30)
            controller.ForceRefresh();
            yield return null;

            Assert.AreEqual("boys_lockers", controller.ActiveZoneId);
            AssertOnlyHidden("boys_lockers");
            AssertCoverVisible("gym", true);
        }

        [UnityTest]
        public IEnumerator Doorway_Threshold_Is_Stable()
        {
            // West corridor cell (9,7) is hall; ms_classroom_1 cell (10,7) is the room.
            PlacePlayer(109.5f, 7.5f);
            controller.ForceRefresh();
            yield return null;
            Assert.AreEqual(SchoolZoneLayout.HallId, controller.ActiveZoneId);

            PlacePlayer(110.5f, 7.5f);
            controller.ForceRefresh();
            yield return null;
            Assert.AreEqual("ms_classroom_1", controller.ActiveZoneId);
            AssertOnlyHidden("ms_classroom_1");

            PlacePlayer(109.5f, 7.5f);
            controller.ForceRefresh();
            yield return null;
            Assert.AreEqual(SchoolZoneLayout.HallId, controller.ActiveZoneId);
            AssertOnlyHidden(SchoolZoneLayout.HallId);
        }

        [UnityTest]
        public IEnumerator Portal_Arrival_At_The_Entrance_Reveals_The_Hall()
        {
            PortalManager manager = PortalManager.Instance;
            Assert.IsNotNull(manager);

            Assert.IsTrue(manager.TryUsePortal("school_gate", player.transform),
                "the foyer portal should teleport the player to the school.");
            yield return null;

            controller.ForceRefresh();
            yield return null;

            Assert.AreEqual(SchoolZoneLayout.HallId, controller.ActiveZoneId,
                "arriving at the school entrance (a hallway cell) must reveal the hall.");
            AssertOnlyHidden(SchoolZoneLayout.HallId);
            Assert.Less(Vector3.Distance(player.transform.position, new Vector3(150.5f, 0f, 5.5f)), 1.5f);
        }

        private void PlacePlayer(float worldX, float worldZ)
        {
            var target = new Vector3(worldX, player.transform.position.y, worldZ);
            player.transform.position = target;
            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body != null)
                body.position = target;
            Physics.SyncTransforms();
        }

        private void AssertOnlyHidden(string activeZoneId)
        {
            bool revealedSomething = false;
            foreach (SchoolZoneCover cover in covers)
            {
                if (cover.ZoneId == activeZoneId)
                {
                    Assert.IsFalse(cover.IsVisible, "zone '" + cover.ZoneId + "' should be revealed.");
                    revealedSomething = true;
                }
                else
                {
                    Assert.IsTrue(cover.IsVisible, "zone '" + cover.ZoneId + "' should stay covered.");
                }
            }

            if (!string.IsNullOrEmpty(activeZoneId))
                Assert.IsTrue(revealedSomething, "no cover matched the active zone '" + activeZoneId + "'.");
        }

        private void AssertCoverVisible(string zoneId, bool expected)
        {
            foreach (SchoolZoneCover cover in covers)
            {
                if (cover.ZoneId == zoneId)
                {
                    Assert.AreEqual(expected, cover.IsVisible, "zone '" + zoneId + "' visibility.");
                    return;
                }
            }

            Assert.Fail("no cover found for zone '" + zoneId + "'.");
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
