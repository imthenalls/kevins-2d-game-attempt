using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for equipment stat-bonus accumulation.</summary>
    public class StatBonusesTests
    {
        [Test]
        public void Add_Accumulates()
        {
            var b = new StatBonuses();
            b.Add(3, 2);
            b.Add(1, 4);

            Assert.AreEqual(4, b.Attack);
            Assert.AreEqual(6, b.Defense);
        }

        [Test]
        public void Remove_Subtracts_And_Clamps_At_Zero()
        {
            var b = new StatBonuses();
            b.Add(5, 5);

            b.Remove(2, 1);
            Assert.AreEqual(3, b.Attack);
            Assert.AreEqual(4, b.Defense);

            b.Remove(100, 100);
            Assert.AreEqual(0, b.Attack);
            Assert.AreEqual(0, b.Defense);
        }

        [Test]
        public void Clear_Resets()
        {
            var b = new StatBonuses();
            b.Add(3, 3);
            b.Clear();

            Assert.AreEqual(0, b.Attack);
            Assert.AreEqual(0, b.Defense);
        }
    }
}
