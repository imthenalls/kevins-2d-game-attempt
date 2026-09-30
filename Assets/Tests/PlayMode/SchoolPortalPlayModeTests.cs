using System.Collections;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode coverage of the school interior route. The school is content inside the existing
    /// Town interior scene, so the foyer's <c>school_gate</c> portal and the school's
    /// <c>school_entrance</c> portal are a same-scene pair: using one teleports the player within the
    /// already-loaded scene, without a scene load. Also checks the foyer's existing Town connection
    /// and that every principal school room's doorway is physically reachable.
    /// </summary>
    public class SchoolPortalPlayModeTests : PlayModeTestBase
    {
        private const string TownScene = "Assets/Scenes/Town.unity";

        [UnityTest]
        public IEnumerator Foyer_And_School_Portals_Are_Wired_In_Same_Scene()
        {
            yield return LoadScene(TownScene);

            PortalManager manager = PortalManager.Instance;
            Assert.IsNotNull(manager, "GameBootstrap should provide a PortalManager.");

            Assert.IsTrue(manager.TryFindPortal("school_gate", out IPortalRoute toSchool),
                "the foyer must contain the school_gate portal.");
            Assert.IsTrue(string.IsNullOrEmpty(toSchool.DestinationScene),
                "the school route must stay in this scene (no Destination Scene).");
            Assert.AreEqual("school_entrance", toSchool.DestinationPortalId);
            Assert.IsFalse(toSchool.ChangesWorld, "the school route stays on World A.");

            Assert.IsTrue(manager.TryFindPortal("school_entrance", out IPortalRoute toFoyer),
                "the school must contain the school_entrance return portal.");
            Assert.IsTrue(string.IsNullOrEmpty(toFoyer.DestinationScene));
            Assert.AreEqual("school_gate", toFoyer.DestinationPortalId);

            // The foyer keeps the existing World B portal, and the Town <-> foyer door is untouched.
            Assert.IsTrue(manager.TryFindPortal("world_b_portal", out _),
                "the foyer must still contain the world_b_portal portal.");
            Assert.IsTrue(manager.TryFindPortal("main_building_door", out IPortalRoute townDoor));
            Assert.AreEqual("int_20_14", townDoor.DestinationPortalId);
            Assert.IsTrue(manager.TryFindPortal("int_20_14", out IPortalRoute roomDoor));
            Assert.AreEqual("main_building_door", roomDoor.DestinationPortalId);
        }

        [UnityTest]
        public IEnumerator Foyer_School_RoundTrip_Teleports_Without_Loading_A_Scene()
        {
            yield return LoadScene(TownScene);

            PortalManager manager = PortalManager.Instance;
            WorldTravelState travel = WorldTravelState.Instance;
            Assert.IsNotNull(manager);
            Assert.IsNotNull(travel);
            Assert.AreEqual(WorldLayer.WorldA, travel.CurrentWorld);

            int sceneHandle = SceneManager.GetActiveScene().handle;

            PlayerController3D player = Object.FindAnyObjectByType<PlayerController3D>();
            Assert.IsNotNull(player, "Town must have a 3D player.");

            Assert.IsTrue(manager.TryFindPortal("school_entrance", out IPortalRoute schoolEntrance));
            Assert.IsTrue(manager.TryFindPortal("school_gate", out IPortalRoute foyerGate));

            Assert.IsTrue(manager.TryUsePortal("school_gate", player.transform), "the foyer portal should teleport the player.");
            yield return null;

            Assert.IsTrue(sceneHandle == SceneManager.GetActiveScene().handle,
                "the school is in the same scene; no scene should be loaded.");
            Assert.AreEqual("Town", SceneManager.GetActiveScene().name);
            Assert.AreEqual(WorldLayer.WorldA, travel.CurrentWorld);
            Assert.Less(Vector3.Distance(player.transform.position, schoolEntrance.ArrivalPosition), 1.5f,
                "the player must arrive at the school entrance; was " + player.transform.position);

            yield return WaitForCooldown();

            Assert.IsTrue(manager.TryUsePortal("school_entrance", player.transform), "the school portal should return the player.");
            yield return null;

            Assert.IsTrue(sceneHandle == SceneManager.GetActiveScene().handle);
            Assert.AreEqual("Town", SceneManager.GetActiveScene().name);
            Assert.AreEqual(WorldLayer.WorldA, travel.CurrentWorld);
            Assert.Less(Vector3.Distance(player.transform.position, foyerGate.ArrivalPosition), 1.5f,
                "the player must arrive back in the foyer; was " + player.transform.position);
            Assert.IsTrue(InsideMainBuildingRoom(player.transform.position),
                "the foyer arrival point must be inside the main building room.");
        }

        [UnityTest]
        public IEnumerator Every_School_Room_Doorway_Is_Reachable()
        {
            yield return LoadScene(TownScene);

            PortalManager manager = PortalManager.Instance;
            Assert.IsTrue(manager.TryFindPortal("school_entrance", out IPortalRoute schoolEntrance));

            SchoolRoomMarker[] markers =
                Object.FindObjectsByType<SchoolRoomMarker>(FindObjectsInactive.Exclude);
            Assert.Greater(markers.Length, 20, "expected a room marker for every principal room.");

            int wallMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Walls"));
            HashSet<Vector2Int> reached = FloodWalkable(WorldToCell(schoolEntrance.ArrivalPosition), wallMask);

            foreach (SchoolRoomMarker marker in markers)
            {
                Vector2Int cell = WorldToCell(marker.transform.position);
                Assert.IsTrue(reached.Contains(cell),
                    "room '" + marker.RoomId + "' is not reachable from the school entrance (cell " + cell + ").");
            }
        }

        // Flood fill on a 1-unit grid; a cell is blocked when a Walls-layer collider overlaps its
        // centre. Wall boxes sit 0.15 into a cell edge, so a 0.3 half-extent probe stays clear of them
        // while still catching props and sealed doorways.
        private static HashSet<Vector2Int> FloodWalkable(Vector2Int start, int wallMask)
        {
            var reached = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            var directions = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                foreach (Vector2Int dir in directions)
                {
                    Vector2Int next = cell + dir;
                    if (reached.Contains(next) || next.x < 90 || next.x > 300 || next.y < -4 || next.y > 80)
                        continue;

                    Vector3 center = new Vector3(next.x + 0.5f, 0.5f, next.y + 0.5f);
                    if (Physics.CheckBox(center, new Vector3(0.3f, 0.5f, 0.3f), Quaternion.identity, wallMask))
                        continue;

                    reached.Add(next);
                    queue.Enqueue(next);
                }
            }

            return reached;
        }

        private static Vector2Int WorldToCell(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.z));

        // The Town foyer (Main Building Room): x 24..34, z -15..-8.
        private static bool InsideMainBuildingRoom(Vector3 position) =>
            position.x >= 24.5f && position.x <= 33.5f && position.z >= -14.5f && position.z <= -8.5f;

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
