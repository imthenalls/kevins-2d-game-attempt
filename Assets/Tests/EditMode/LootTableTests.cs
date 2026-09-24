using System;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the loot quantity roll.</summary>
    public class LootTableTests
    {
        [Test]
        public void Roll_Stays_Within_Range()
        {
            var rng = new Random(123);
            for (int i = 0; i < 1000; i++)
            {
                int quantity = LootTable.RollQuantity(2, 5, rng);
                Assert.GreaterOrEqual(quantity, 2);
                Assert.LessOrEqual(quantity, 5);
            }
        }

        [Test]
        public void Fixed_Range_Returns_That_Value() =>
            Assert.AreEqual(4, LootTable.RollQuantity(4, 4, new Random(1)));

        [Test]
        public void Inverted_Range_Clamps_To_Minimum() =>
            Assert.AreEqual(7, LootTable.RollQuantity(7, 3, new Random(1)));

        [Test]
        public void Negative_Minimum_Clamps_To_Zero() =>
            Assert.AreEqual(0, LootTable.RollQuantity(-5, 0, new Random(1)));
    }
}
