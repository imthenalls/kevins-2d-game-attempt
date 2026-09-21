using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Guards the per-scene UI and camera invariants that the boot layer is responsible for, so a new
    /// scene cannot silently ship with a camera that does not follow or with no working inventory UI:
    ///
    ///   - exactly one enabled camera tagged MainCamera, and it has CameraFollow
    ///   - a live EventSystem and a keyboard device (otherwise no UI input at all)
    ///   - an InventoryUI whose panel actually opens, and is not input-locked
    ///   - the camera converges on the player
    ///
    /// These assert the *runtime* outcome, so they stay valid whether a scene authors its own UI or
    /// relies on the shared `Assets/Resources/InventoryCanvas.prefab`.
    ///
    /// Unity setup: none. The scenes must be enabled in Build Settings.
    /// </summary>
    public class SceneUiCameraPlayModeTests : PlayModeTestBase
    {
        private static readonly string[] Scenes =
        {
            "Assets/Scenes/Overworld.unity",
            "Assets/Scenes/WorldB.unity",
            "Assets/Scenes/Town.unity",
        };

        [UnityTest]
        public IEnumerator Every_Scene_Has_One_Enabled_Follow_Camera()
        {
            foreach (string path in Scenes)
            {
                yield return LoadScene(path);
                string where = " in " + path;

                Assert.IsNotNull(Keyboard.current,
                    "No keyboard device (is Active Input Handling set to the Input System package?)" + where);

                int enabledMainCameras = 0;
                Camera mainCamera = null;
                foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                {
                    if (!camera.CompareTag("MainCamera") || !camera.isActiveAndEnabled)
                        continue;

                    enabledMainCameras++;
                    mainCamera = camera;
                }

                Assert.AreEqual(1, enabledMainCameras,
                    "Expected exactly one enabled MainCamera camera; a static one can render over the follow camera" + where);
                Assert.IsTrue(mainCamera.GetComponent<CameraFollow>() != null || mainCamera.GetComponent<IsoCameraRig>() != null,
                    "Main camera has no CameraFollow/IsoCameraRig (GameBootstrap should add one)" + where);
                Assert.IsTrue(mainCamera.orthographic, "Main camera is not orthographic" + where);

                Assert.IsTrue(HasEnabledEventSystem(), "No enabled EventSystem; UI input is dead" + where);
            }
        }

        [UnityTest]
        public IEnumerator Every_Scene_Has_A_Working_Inventory_UI()
        {
            foreach (string path in Scenes)
            {
                yield return LoadScene(path);
                string where = " in " + path;

                InventoryUI ui = Object.FindAnyObjectByType<InventoryUI>();
                Assert.IsNotNull(ui, "No InventoryUI; the I/E hotkeys do nothing" + where);
                Assert.IsTrue(ui.isActiveAndEnabled, "InventoryUI is disabled" + where);
                Assert.IsNotNull(ui.PanelRoot, "InventoryUI.PanelRoot is not assigned" + where);
                Assert.IsFalse(ui.InputLocked, "Inventory hotkeys are locked" + where);

                bool wasOpen = ui.IsOpen;
                ui.Toggle();
                Assert.AreNotEqual(wasOpen, ui.IsOpen, "Toggle() did not change the inventory panel" + where);

                ui.Toggle();
                Assert.AreEqual(wasOpen, ui.IsOpen, "Toggle() did not restore the inventory panel" + where);
            }
        }

        [UnityTest]
        public IEnumerator Camera_Follows_An_Active_Player()
        {
            int checkedScenes = 0;

            foreach (string path in Scenes)
            {
                yield return LoadScene(path);

                Camera camera = Camera.main;
                Assert.IsNotNull(camera, "No Camera.main" + " in " + path);

                PlayerControllerBase player = Object.FindAnyObjectByType<PlayerControllerBase>();
                if (player == null)
                    continue; // scene has no active player for this world; nothing to follow

                for (int frame = 0; frame < 20; frame++)
                    yield return null;

                // The player must sit on the camera's view axis (works for both the flat follow and
                // the offset 3D isometric rig, where the camera is not placed on top of the player).
                Vector3 toPlayer = player.transform.position - camera.transform.position;
                Vector3 perpendicular = toPlayer - Vector3.Project(toPlayer, camera.transform.forward);
                float distance = perpendicular.magnitude;
                Assert.Less(distance, 0.5f,
                    "Camera did not converge on the player in " + path + " (axis distance " + distance + ")");

                checkedScenes++;
            }

            Assert.Greater(checkedScenes, 0, "No scene had an active player; the follow check never ran.");
        }

        private static bool HasEnabledEventSystem()
        {
            foreach (EventSystem eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
            {
                if (eventSystem.isActiveAndEnabled)
                    return true;
            }

            return false;
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
