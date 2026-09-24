using System.Collections;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode integration tests for the runtime systems: portal destination resolution against a
    /// real scene, and world-state fact storage with a snapshot round trip.
    /// </summary>
    public class SystemIntegrationPlayModeTests : PlayModeTestBase
    {
        private const string OverworldScene = "Assets/Scenes/Overworld.unity";

        [UnityTest]
        public IEnumerator Overworld_Resolves_Portal_Destinations()
        {
            yield return LoadScene(OverworldScene);

            Assert.IsNotNull(PortalManager.Instance, "GameBootstrap should provide a persistent PortalManager.");

            Assert.IsTrue(
                PortalManager.Instance.TryFindPortal("world_b_portal", out IPortalRoute portal),
                "The hub portal should exist and be findable by id.");
            Assert.AreEqual("WorldB", portal.DestinationScene);
            Assert.IsFalse(PortalManager.Instance.TryFindPortal("no_such_portal_xyz", out _));

            // PortalManager is persistent now (GameBootstrap) - do not destroy it.
            yield return null;
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
