using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the local-avoidance separation and steering math.</summary>
    public class LocalAvoidanceTests
    {
        [Test]
        public void Separation_Points_Away_From_A_Neighbor()
        {
            var samples = new List<NeighborSample>
            {
                new NeighborSample(1f, 0f, 1f) // neighbor sits on the -X side, so the offset to self is +X
            };

            bool any = LocalAvoidance.TrySeparation(samples, 2f, 123, out float x, out float y);

            Assert.IsTrue(any);
            Assert.Greater(x, 0f);
            Assert.AreEqual(0f, y, 0.0001f);
        }

        [Test]
        public void Separation_Is_Empty_Without_Neighbors()
        {
            Assert.IsFalse(LocalAvoidance.TrySeparation(new List<NeighborSample>(), 2f, 0, out _, out _));
            Assert.IsFalse(LocalAvoidance.TrySeparation(null, 2f, 0, out _, out _));
        }

        [Test]
        public void Separation_Is_Empty_When_Neighbors_Are_Outside_The_Radius()
        {
            var samples = new List<NeighborSample>
            {
                new NeighborSample(5f, 0f, 5f)
            };

            Assert.IsFalse(LocalAvoidance.TrySeparation(samples, 2f, 0, out _, out _));
        }

        [Test]
        public void Stacked_Neighbors_Escape_Along_The_Seed_Angle()
        {
            var samples = new List<NeighborSample>
            {
                new NeighborSample(0f, 0f, 0f)
            };

            Assert.IsTrue(LocalAvoidance.TrySeparation(samples, 1f, 90, out float x, out float y));
            Assert.AreEqual(0f, x, 0.001f);
            Assert.AreEqual(1f, y, 0.001f);
        }

        [Test]
        public void Steer_Returns_Direction_When_No_Separation()
        {
            LocalAvoidance.Steer(1f, 0f, 0f, 0f, 2f, out float x, out float y);

            Assert.AreEqual(1f, x, 0.0001f);
            Assert.AreEqual(0f, y, 0.0001f);
        }

        [Test]
        public void Steer_Blends_And_Normalizes()
        {
            LocalAvoidance.Steer(1f, 0f, 0f, 1f, 1f, out float x, out float y);

            Assert.AreEqual(0.7071f, x, 0.001f);
            Assert.AreEqual(0.7071f, y, 0.001f);
        }
    }
}
