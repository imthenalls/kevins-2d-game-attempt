using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the authoritative health model used by the player (bound through the
    /// GameSession) and reusable by any entity.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class HealthModelTests
    {
        [TestCase(30, 30, 30)]
        [TestCase(30, 99, 30)]
        [TestCase(30, -5, 0)]
        [TestCase(0, 5, 1)]
        public void Constructor_Clamps_Initial_Values(int maxHp, int hp, int expectedHp)
        {
            var model = new HealthModel(maxHp, hp);
            Assert.AreEqual(expectedHp, model.Hp);
        }

        [Test]
        public void Damage_Clamps_And_Kills()
        {
            var model = new HealthModel(10, 10);

            int applied = model.ApplyDamage(999);

            Assert.AreEqual(10, applied);
            Assert.AreEqual(0, model.Hp);
            Assert.IsFalse(model.IsAlive);
            Assert.AreEqual(0, model.ApplyDamage(5), "damage while dead is a no-op");
        }

        [Test]
        public void Heal_Clamps_To_Max_And_Does_Not_Revive()
        {
            var model = new HealthModel(20, 5);

            Assert.AreEqual(15, model.Heal(15));
            Assert.AreEqual(20, model.Hp);

            model.SetHp(0);
            Assert.AreEqual(0, model.Heal(5));
        }

        [Test]
        public void SetMaxHp_Reduces_Hp_To_The_New_Maximum()
        {
            var model = new HealthModel(30, 30);

            model.SetMaxHp(12);

            Assert.AreEqual(12, model.MaxHp);
            Assert.AreEqual(12, model.Hp);
        }

        [Test]
        public void HpChanged_Reports_Current_And_Max()
        {
            var model = new HealthModel(30, 30);
            int seenHp = -1;
            int seenMax = -1;
            model.HpChanged += (hp, max) => { seenHp = hp; seenMax = max; };

            model.ApplyDamage(7);

            Assert.AreEqual(23, seenHp);
            Assert.AreEqual(30, seenMax);
        }
    }
}
