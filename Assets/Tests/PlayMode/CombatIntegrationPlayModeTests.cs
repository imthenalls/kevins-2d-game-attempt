using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode integration tests for the combat receive path: damage application, invincibility,
    /// damage multiplier, and death firing exactly once.
    /// </summary>
    public class CombatIntegrationPlayModeTests : PlayModeTestBase
    {
        private GameObject subject;
        private EntityStats stats;
        private CombatReceiver receiver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            subject = new GameObject("Combat Subject");
            stats = subject.AddComponent<EntityStats>();
            receiver = subject.AddComponent<CombatReceiver>();
            yield return null;
            stats.Configure(50, 10);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (subject != null)
                Object.Destroy(subject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReceiveHit_Damages_Stats_And_Fires_OnHit()
        {
            int hits = 0;
            receiver.OnHit += (_, _) => hits++;

            receiver.ReceiveHit(new DamageInfo(20, null));

            Assert.AreEqual(30, stats.Hp);
            Assert.AreEqual(1, hits);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Invincibility_Blocks_Damage_Until_Cleared()
        {
            receiver.Invincible = true;
            receiver.ReceiveHit(new DamageInfo(20, null));
            Assert.AreEqual(50, stats.Hp);

            receiver.SetInvincible(false);
            receiver.ReceiveHit(new DamageInfo(20, null));
            Assert.AreEqual(30, stats.Hp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Damage_Multiplier_Scales_The_Applied_Hit()
        {
            receiver.DamageMultiplier = 0.5f;

            receiver.ReceiveHit(new DamageInfo(20, null));

            Assert.AreEqual(40, stats.Hp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Death_Fires_Exactly_Once()
        {
            int statDeaths = 0;
            int receiverDeaths = 0;
            stats.OnDeath += () => statDeaths++;
            receiver.OnDeath += _ => receiverDeaths++;

            receiver.ReceiveHit(new DamageInfo(999, null));
            receiver.ReceiveHit(new DamageInfo(999, null));

            Assert.AreEqual(0, stats.Hp);
            Assert.IsFalse(stats.IsAlive);
            Assert.AreEqual(1, statDeaths);
            Assert.AreEqual(1, receiverDeaths);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Disabling_Combat_Blocks_All_Damage()
        {
            receiver.CombatEnabled = false;

            receiver.ReceiveHit(new DamageInfo(50, null));

            Assert.AreEqual(50, stats.Hp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Configure_Updates_A_Bound_Health_Model()
        {
            // Mirrors an enemy: a saveable model is bound (NpcStateView) before NpcController's
            // Configure runs. Configure must reach the bound model, not just the private fallback.
            var model = new HealthModel(100, 100);
            stats.BindHealthModel(model);

            stats.Configure(40, 5);

            Assert.AreEqual(40, model.MaxHp, "bound model maxHp must be configured");
            Assert.AreEqual(40, model.Hp, "bound model hp must be configured");
            Assert.AreEqual(40, stats.MaxHp);
            Assert.AreEqual(40, stats.Hp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bound_Model_Adopts_Configured_Values()
        {
            stats.Configure(40, 5);

            var model = new HealthModel(stats.MaxHp, stats.Hp);
            stats.BindHealthModel(model);

            Assert.AreEqual(40, stats.MaxHp);
            Assert.AreEqual(40, stats.Hp);
            yield return null;
        }
    }
}
