using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies save validation rejects unsupported versions, missing scenes, and negative stats, and
    /// accepts a well-formed save for a scene that is actually available.
    /// </summary>
    public class SaveSafetyPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Valid_Save_Passes_Validation()
        {
            var data = new SaveData { saveVersion = 8, currentScene = "Overworld", playerMaxHp = 100, playerHp = 40, playerMaxMp = 50, playerMp = 10 };

            Assert.IsNull(SaveManager.ValidateSave(data));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Unsupported_Version_Is_Rejected()
        {
            var data = new SaveData { saveVersion = 999, currentScene = "Overworld", playerMaxHp = 100 };

            Assert.IsNotNull(SaveManager.ValidateSave(data));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Missing_Scene_Is_Rejected()
        {
            var data = new SaveData { saveVersion = 8, currentScene = "", playerMaxHp = 100 };

            Assert.IsNotNull(SaveManager.ValidateSave(data));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Negative_Stats_Are_Rejected()
        {
            var data = new SaveData { saveVersion = 8, currentScene = "Overworld", playerMaxHp = 100, playerHp = -1 };

            Assert.IsNotNull(SaveManager.ValidateSave(data));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Normalize_Fills_Null_Collections()
        {
            var data = new SaveData();
            data.npcStates = null;
            data.worldFacts = null;
            data.pendingRewards = null;

            SaveManager.NormalizeSave(data);

            Assert.IsNotNull(data.npcStates);
            Assert.IsNotNull(data.worldFacts);
            Assert.IsNotNull(data.pendingRewards);
            yield return null;
        }
    }
}
