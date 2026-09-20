using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Guards the shared inventory canvas prefab in `Resources`. Scenes that do not author their own
    /// UI (for example Town) get this prefab instantiated by `GameUI`, so if it is deleted, emptied,
    /// or loses its wired `InventoryUI`, the `I`/`E` hotkeys silently stop working in those scenes.
    ///
    /// Unity setup: none. Runs in Edit Mode.
    /// </summary>
    public class SharedInventoryCanvasPrefabTests
    {
        private const string PrefabPath = "Assets/Resources/InventoryCanvas.prefab";
        private const string ResourceName = "InventoryCanvas";

        [Test]
        public void Shared_Inventory_Canvas_Prefab_Exists()
        {
            Assert.IsNotNull(
                Resources.Load<GameObject>(ResourceName),
                "Missing " + PrefabPath + " - scenes without their own UI rely on it for I/E hotkeys.");
        }

        [Test]
        public void Shared_Inventory_Canvas_Prefab_Has_A_Wired_Inventory_UI()
        {
            GameObject prefab = Resources.Load<GameObject>(ResourceName);
            Assert.IsNotNull(prefab, "Missing " + PrefabPath + ".");

            InventoryUI ui = prefab.GetComponentInChildren<InventoryUI>(true);
            Assert.IsNotNull(ui, "The shared canvas prefab has no InventoryUI.");

            FieldInfo panelRoot = typeof(InventoryUI).GetField(
                "panelRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(panelRoot, "InventoryUI.panelRoot was renamed; update this test.");
            Assert.IsNotNull(panelRoot.GetValue(ui),
                "InventoryUI.panelRoot is not assigned in the shared prefab; the panel cannot open.");

            Assert.IsNotNull(prefab.GetComponentInChildren<Canvas>(true),
                "The shared canvas prefab has no Canvas.");
        }
    }
}
