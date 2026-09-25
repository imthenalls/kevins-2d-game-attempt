using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Verifies the DamageFlash component flashes the body sprite on hit and restores the resting
    /// color afterwards — instant hit/damage feedback.
    /// </summary>
    public class DamageFlashPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Hit_Flashes_The_Body_Then_Restores_The_Resting_Color()
        {
            var subject = new GameObject("Flash Subject");
            var stats = subject.AddComponent<EntityStats>();
            var receiver = subject.AddComponent<CombatReceiver>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(subject.transform);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.color = Color.green;

            var flash = subject.AddComponent<DamageFlash>();
            yield return null;

            stats.Configure(50, 10);
            receiver.ReceiveHit(new DamageInfo(10, null));
            yield return null;

            Assert.AreEqual(Color.white, renderer.color, "a hit must flash the body");

            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(Color.green, renderer.color, "the flash must restore the resting color");

            Object.Destroy(subject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator No_Flash_When_Combat_Is_Disabled()
        {
            var subject = new GameObject("Flash Subject");
            var stats = subject.AddComponent<EntityStats>();
            var receiver = subject.AddComponent<CombatReceiver>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(subject.transform);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.color = Color.green;

            subject.AddComponent<DamageFlash>();
            yield return null;

            stats.Configure(50, 10);
            receiver.CombatEnabled = false;
            receiver.ReceiveHit(new DamageInfo(10, null));
            yield return null;

            Assert.AreEqual(Color.green, renderer.color, "a blocked hit must not flash");

            Object.Destroy(subject);
            yield return null;
        }
    }
}
