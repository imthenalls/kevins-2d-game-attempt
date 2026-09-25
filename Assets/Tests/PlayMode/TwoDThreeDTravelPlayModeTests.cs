using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies the 2D ↔ 3D scene boundary: the 2D scenes (Overworld / WorldB) use a 2D player with a
    /// Rigidbody2D and a flat CameraFollow, while the 3D Town uses a 3D player with a Rigidbody and an
    /// IsoCameraRig. The shared inventory also survives the transition, so the avatar, movement body,
    /// camera, and inventory are all correct after each switch.
    /// </summary>
    public class TwoDThreeDTravelPlayModeTests : PlayModeTestBase
    {
        private const string OverworldScene = "Assets/Scenes/Overworld.unity";
        private const string TownScene = "Assets/Scenes/Town.unity";

        [UnityTest]
        public IEnumerator TwoD_And_3D_Scenes_Use_The_Right_Avatar_And_Movement_Body()
        {
            yield return LoadScene(OverworldScene);

            PlayerController2D player2D = Object.FindAnyObjectByType<PlayerController2D>();
            Assert.IsNotNull(player2D, "Overworld must have a 2D player");
            Assert.IsNotNull(player2D.GetComponent<Rigidbody2D>(), "2D player must move with a Rigidbody2D");
            Assert.IsNull(player2D.GetComponent<Rigidbody>(), "2D player must not carry a 3D Rigidbody");
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController3D>(), "Overworld must not have a 3D player");

            yield return LoadScene(TownScene);

            PlayerController3D player3D = Object.FindAnyObjectByType<PlayerController3D>();
            Assert.IsNotNull(player3D, "Town must have a 3D player");
            Assert.IsNotNull(player3D.GetComponent<Rigidbody>(), "3D player must move with a Rigidbody");
            Assert.IsNull(player3D.GetComponent<Rigidbody2D>(), "3D player must not carry a Rigidbody2D");
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController2D>(), "Town must not have a 2D player");

            yield return LoadScene(OverworldScene);

            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerController2D>(), "returning to Overworld restores the 2D player");
            Assert.IsNull(Object.FindAnyObjectByType<PlayerController3D>(), "returning to Overworld removes the 3D player");
        }

        [UnityTest]
        public IEnumerator TwoD_Scenes_Use_Flat_Follow_And_3D_Town_Uses_Iso_Rig()
        {
            yield return LoadScene(OverworldScene);
            Camera camera2D = Camera.main;
            Assert.IsNotNull(camera2D);
            Assert.IsNotNull(camera2D.GetComponent<CameraFollow>(), "2D scenes use the flat follow camera");
            Assert.IsNull(camera2D.GetComponent<IsoCameraRig>(), "2D scenes must not use the isometric rig");

            yield return LoadScene(TownScene);
            Camera camera3D = Camera.main;
            Assert.IsNotNull(camera3D);
            Assert.IsNotNull(camera3D.GetComponent<IsoCameraRig>(), "the 3D Town uses the isometric rig");
            Assert.IsNull(camera3D.GetComponent<CameraFollow>(), "the 3D Town must not use the flat follow camera");
        }

        [UnityTest]
        public IEnumerator Inventory_Model_Persists_Across_The_2D_To_3D_Transition()
        {
            yield return LoadScene(OverworldScene);

            InventoryUI ui = InventoryUI.Instance;
            Assert.IsNotNull(ui, "InventoryUI must be present");
            InventoryModel worldA = ui.GetInventoryForWorld(WorldLayer.WorldA);
            Assert.IsNotNull(worldA, "World A inventory must exist");

            yield return LoadScene(TownScene);

            InventoryUI uiAfter = InventoryUI.Instance;
            Assert.IsNotNull(uiAfter, "InventoryUI must survive the scene change");
            InventoryModel worldAAfter = uiAfter.GetInventoryForWorld(WorldLayer.WorldA);
            Assert.AreSame(worldA, worldAAfter,
                "the World A inventory model must persist (not be recreated) across the 2D -> 3D transition");
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
