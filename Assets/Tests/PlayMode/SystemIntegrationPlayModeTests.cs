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
    /// Play Mode integration tests for the runtime systems: the Town ↔ WorldB portal routing against
    /// real scenes, and world-state fact storage with a snapshot round trip.
    /// </summary>
    public class SystemIntegrationPlayModeTests : PlayModeTestBase
    {
        private const string OverworldScene = "Assets/Scenes/Overworld.unity";
        private const string TownScene = "Assets/Scenes/Town.unity";
        private const string WorldBScene = "Assets/Scenes/WorldB.unity";

        [UnityTest]
        public IEnumerator World_B_Route_Is_Authored_From_Town_Not_Overworld()
        {
            yield return LoadScene(OverworldScene);

            PortalManager manager = PortalManager.Instance;
            Assert.IsNotNull(manager, "GameBootstrap should provide a persistent PortalManager.");
            Assert.IsFalse(
                manager.TryFindPortal("world_b_portal", out _),
                "Overworld must not contain world_b_portal; the route lives in the Town.");

            yield return LoadScene(TownScene);

            Assert.IsTrue(
                manager.TryFindPortal("world_b_portal", out IPortalRoute toWorldB),
                "Town must contain the world_b_portal route.");
            Assert.AreEqual("WorldB", toWorldB.DestinationScene);
            Assert.AreEqual("world_b_entry", toWorldB.DestinationPortalId);
            Assert.IsTrue(toWorldB.ChangesWorld);
            Assert.AreEqual(WorldLayer.WorldB, toWorldB.DestinationWorld);

            int count = 0;
            foreach (PortalTrigger3D portal in Object.FindObjectsByType<PortalTrigger3D>(FindObjectsInactive.Include))
            {
                if (portal.PortalId == "world_b_portal")
                    count++;
            }
            Assert.AreEqual(1, count, "Town must contain exactly one world_b_portal.");
            Assert.IsTrue(
                IsUnderMainBuildingRoom(toWorldB.Self.transform),
                "the world_b_portal must be authored under Main Building Room.");

            yield return LoadScene(WorldBScene);

            Assert.IsTrue(
                manager.TryFindPortal("world_b_entry", out IPortalRoute toTown),
                "WorldB must contain the world_b_entry return route.");
            Assert.AreEqual("Town", toTown.DestinationScene);
            Assert.AreEqual("world_b_portal", toTown.DestinationPortalId);
            Assert.IsTrue(toTown.ChangesWorld);
            Assert.AreEqual(WorldLayer.WorldA, toTown.DestinationWorld);
        }

        [UnityTest]
        public IEnumerator Town_WorldB_RoundTrip_Returns_To_Main_Building_Room()
        {
            yield return LoadScene(TownScene);

            PortalManager manager = PortalManager.Instance;
            Assert.IsNotNull(manager);
            WorldTravelState travel = WorldTravelState.Instance;
            Assert.IsNotNull(travel);

            PlayerController3D player3D = Object.FindAnyObjectByType<PlayerController3D>();
            Assert.IsNotNull(player3D, "Town must have a 3D player.");

            Assert.IsTrue(manager.TryFindPortal("world_b_portal", out IPortalRoute toWorldB));
            Assert.IsTrue(manager.TryUsePortal(toWorldB, player3D.transform), "travel to WorldB should start.");

            yield return WaitForScene("WorldB");
            Assert.AreEqual(WorldLayer.WorldB, travel.CurrentWorld, "arriving in WorldB must activate World B.");
            Assert.IsNotNull(
                Object.FindAnyObjectByType<PlayerController2D>(),
                "WorldB must have an active 2D player after traveling.");

            yield return WaitForCooldown();

            Assert.IsTrue(
                manager.TryFindPortal("world_b_entry", out IPortalRoute toTown),
                "WorldB must contain the world_b_entry return route.");
            Transform traveler = Object.FindAnyObjectByType<PlayerController2D>().transform;
            Assert.IsTrue(manager.TryUsePortal(toTown, traveler), "travel back to Town should start.");

            yield return WaitForScene("Town");
            Assert.AreEqual(WorldLayer.WorldA, travel.CurrentWorld, "returning to Town must activate World A.");

            PlayerController3D returned = Object.FindAnyObjectByType<PlayerController3D>();
            Assert.IsNotNull(returned, "Town must have a 3D player after the round trip.");
            Assert.IsTrue(
                IsInsideMainBuildingRoom(returned.transform.position),
                "the player must arrive inside the Main Building Room; was " + returned.transform.position);
        }

        [UnityTest]
        public IEnumerator World_Facts_RoundTrip_Through_A_Snapshot()
        {
            // WorldStateManager is created by GameBootstrap now; use the persistent instance.
            WorldStateManager state = WorldStateManager.Instance;
            Assert.IsNotNull(state, "GameBootstrap should provide a persistent WorldStateManager.");
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

            // WorldStateManager is persistent (GameBootstrap) - do not destroy it.
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_Health_Is_Model_Backed()
        {
            var subject = new GameObject("Player Test Subject", typeof(Rigidbody2D));
            EntityStats stats = subject.AddComponent<EntityStats>();
            subject.AddComponent<PlayerController2D>(); // Awake binds player HP to the session model
            yield return null;

            GameSession session = GameSessionHost.Session;
            Assert.IsNotNull(session);
            Assert.IsNotNull(session.PlayerHealth, "The player should bind a session-owned health model.");

            Assert.AreEqual(session.PlayerHealth.Hp, stats.Hp);
            Assert.AreEqual(session.PlayerHealth.MaxHp, stats.MaxHp);

            session.PlayerHealth.SetHp(session.PlayerHealth.MaxHp);
            int before = session.PlayerHealth.Hp;
            stats.TakeDamage(3);
            Assert.AreEqual(before - 3, session.PlayerHealth.Hp, "damage must flow into the session model");

            Object.Destroy(subject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scene_Player_Binds_To_Session_Health()
        {
            // WorldTravelState is DontDestroyOnLoad and keeps CurrentWorld across PlayMode tests, so
            // an earlier test that loaded WorldB would deactivate Overworld's World A player
            // (WorldCharacter.SetActiveForWorld). Reset the world before loading so this test does
            // not depend on execution order.
            WorldTravelState travel = WorldTravelState.Instance;
            if (travel != null)
                travel.SetCurrentWorld(WorldLayer.WorldA);

            yield return LoadScene(OverworldScene);

            PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
            Assert.IsNotNull(player, "Overworld should contain an active player when the current world is World A.");

            GameSession session = GameSessionHost.Session;
            Assert.IsNotNull(session);
            Assert.IsNotNull(session.PlayerHealth, "The scene player should bind a session-owned health model.");
            Assert.AreEqual(session.PlayerHealth.Hp, player.Stats.Hp);
            Assert.AreEqual(session.PlayerHealth.MaxHp, player.Stats.MaxHp);
        }

        [UnityTest]
        public IEnumerator Player_Position_Is_Model_Backed_And_Teleportable()
        {
            yield return LoadScene(OverworldScene);

            PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
            Assert.IsNotNull(player, "Overworld should contain an active player when the current world is World A.");

            GameSession session = GameSessionHost.Session;
            Assert.IsNotNull(session);
            PositionModel model = session.PlayerPosition;
            Assert.IsNotNull(model, "The player should bind a session-owned position model.");

            Grid grid = Object.FindAnyObjectByType<Grid>();
            Assert.IsNotNull(grid);

            // The model mirrors the transform's logical cell.
            Vector3Int current = grid.WorldToCell(player.transform.position);
            Assert.AreEqual(current.x, model.CellX);
            Assert.AreEqual(current.y, model.CellY);

            // An external model change (load / teleport) moves the body, without waiting a frame.
            int savedX = model.CellX;
            int savedY = model.CellY;
            float savedOffsetX = model.OffsetX;
            float savedOffsetY = model.OffsetY;

            model.Set(current.x + 3, current.y, 0f, 0f);
            Assert.AreEqual(current.x + 3, grid.WorldToCell(player.transform.position).x);

            model.Set(savedX, savedY, savedOffsetX, savedOffsetY);
            Assert.AreEqual(current.x, grid.WorldToCell(player.transform.position).x);
        }

        [UnityTest]
        public IEnumerator World_Remembered_Position_RoundTrips_As_Cell()
        {
            yield return LoadScene(OverworldScene);

            WorldTravelState travel = WorldTravelState.Instance;
            Assert.IsNotNull(travel);

            PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
            Assert.IsNotNull(player);

            Vector3 spot = player.transform.position;
            travel.RememberTravelerPosition(player.transform);

            Assert.IsTrue(travel.TryGetRememberedPosition(travel.CurrentWorld, out _, out Vector3 restored),
                "the traveler position should be remembered");
            Assert.AreEqual(spot.x, restored.x, 0.05f);
            Assert.AreEqual(spot.y, restored.y, 0.05f);

            var entries = new List<WorldPositionSaveEntry>();
            travel.WritePositions(entries);
            Assert.IsTrue(entries.Count > 0);
            Assert.IsTrue(entries.Exists(e => e.hasCell), "remembered positions should save as grid cells");
        }

        // True when the transform sits anywhere under the Town's "Main Building Room" GameObject.
        private static bool IsUnderMainBuildingRoom(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current.name == "Main Building Room")
                    return true;
            }

            return false;
        }

        // The Main Building Room occupies cells x 24..33, z -15..-9 (world x 24..34, z -15..-8);
        // the arrive point is (26.5, -13.5). A half-cell margin keeps the check clear of the walls.
        private static bool IsInsideMainBuildingRoom(Vector3 position)
        {
            return position.x >= 24.5f && position.x <= 33.5f &&
                   position.z >= -14.5f && position.z <= -8.5f;
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
