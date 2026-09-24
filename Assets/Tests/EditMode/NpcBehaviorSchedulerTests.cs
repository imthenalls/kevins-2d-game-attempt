using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for weighted NPC behavior selection.</summary>
    public class NpcBehaviorSchedulerTests
    {
        [Test]
        public void Empty_Weights_Return_Minus_One() =>
            Assert.AreEqual(-1, new NpcBehaviorScheduler(1).PickNext(new float[0]));

        [Test]
        public void Single_Weight_Is_Always_Chosen()
        {
            var s = new NpcBehaviorScheduler(1);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(0, s.PickNext(new[] { 5f }));
        }

        [Test]
        public void Invalid_Weights_Are_Never_Chosen()
        {
            var s = new NpcBehaviorScheduler(7);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(1, s.PickNext(new[] { 0f, 1f, -3f }));
        }

        [Test]
        public void Heavier_Weight_Is_Chosen_More_Often()
        {
            var s = new NpcBehaviorScheduler(123);
            int light = 0, heavy = 0;
            for (int i = 0; i < 2000; i++)
            {
                if (s.PickNext(new[] { 1f, 3f }) == 0) light++;
                else heavy++;
            }

            Assert.Greater(heavy, light);
            Assert.Greater(light, 0, "the light weight should still be chosen sometimes");
        }

        [Test]
        public void All_Invalid_Weights_Fall_Back_To_The_Last()
        {
            var s = new NpcBehaviorScheduler(1);
            Assert.AreEqual(2, s.PickNext(new[] { 0f, 0f, 0f }));
        }
    }
}
