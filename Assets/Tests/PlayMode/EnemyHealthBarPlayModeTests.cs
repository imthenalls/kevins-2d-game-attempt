using System.Collections;
using System.Reflection;
using Game.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode integration tests for the UGUI enemy health bar that replaced the IMGUI OnGUI
    /// implementation: bars spawn only for enemies with the feature enabled, the fill tracks HP
    /// ratio, and the bar hides itself when the enemy dies.
    /// </summary>
    public class EnemyHealthBarPlayModeTests : PlayModeTestBase
    {
        private GameObject cameraObject;
        private GameObject subject;
        private NpcController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            subject = new GameObject("Health Bar Target");
            subject.AddComponent<EntityStats>();
            controller = subject.AddComponent<NpcController>();

            SetPrivateField(controller, "npcType", NpcType.Enemy);
            InvokePrivate(controller, "Awake");
            InvokePrivate(controller, "Start");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (subject != null)
                Object.Destroy(subject);
            if (cameraObject != null)
                Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Enemy_Spawns_A_Health_Bar_That_Tracks_Hp()
        {
            EnemyHealthBarUI bar = Object.FindAnyObjectByType<EnemyHealthBarUI>();
            Assert.IsNotNull(bar, "Enemy NPC should spawn a health bar in Start.");

            RectTransform root = (RectTransform)bar.transform;
            float configuredWidth = Mathf.Round(root.sizeDelta.x);

            Image fill = GetPrivateField<Image>(bar, "fill");
            yield return null;
            Assert.AreEqual(expectedFillWidth(configuredWidth, 1f), fill.rectTransform.sizeDelta.x, 1.5f);

            subject.GetComponent<CombatReceiver>().ReceiveHit(new DamageInfo(15, null));
            yield return null;

            TextMeshProUGUI label = GetPrivateField<TextMeshProUGUI>(bar, "label");
            Assert.AreEqual("15 / 30", label.text);
            Assert.AreEqual(expectedFillWidth(configuredWidth, 0.5f), fill.rectTransform.sizeDelta.x, 1.5f);
        }

        [UnityTest]
        public IEnumerator Bar_Hides_When_The_Enemy_Dies()
        {
            EnemyHealthBarUI bar = Object.FindAnyObjectByType<EnemyHealthBarUI>();
            Assert.IsNotNull(bar);

            subject.GetComponent<CombatReceiver>().ReceiveHit(new DamageInfo(999, null));
            yield return null;

            RectTransform visual = GetPrivateField<RectTransform>(bar, "visual");
            Assert.IsFalse(visual.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator Non_Enemies_And_Disabled_Config_Spawn_Nothing()
        {
            // Subject setup already ran as an Enemy; reconfigure and rebuild.
            NpcControllerConfig config = GetConfig(controller);
            config.ShowEnemyHealthBar = false;
            DestroyBars();

            InvokePrivate(controller, "Awake");
            InvokePrivate(controller, "Start");
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<EnemyHealthBarUI>(), "Disabled config must not spawn a bar.");

            config.ShowEnemyHealthBar = true;
            SetPrivateField(controller, "npcType", NpcType.Generic);
            InvokePrivate(controller, "Awake");
            InvokePrivate(controller, "Start");
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<EnemyHealthBarUI>(), "Only Enemy NPCs spawn a bar.");
        }

        private static float expectedFillWidth(float width, float ratio) => (width - 4f) * ratio;

        private static void DestroyBars()
        {
            foreach (EnemyHealthBarUI bar in Object.FindObjectsByType<EnemyHealthBarUI>(FindObjectsSortMode.None))
                Object.Destroy(bar.gameObject);
        }

        private static NpcControllerConfig GetConfig(NpcController controller)
        {
            return (NpcControllerConfig)typeof(NpcController)
                .GetField("config", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            typeof(NpcController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void InvokePrivate(object target, string name)
        {
            typeof(NpcController)
                .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Invoke(target, null);
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            return (T)typeof(EnemyHealthBarUI)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}
