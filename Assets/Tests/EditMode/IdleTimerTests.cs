using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the idle-duration timer.</summary>
    public class IdleTimerTests
    {
        [Test]
        public void Completes_After_Ticking_Past_The_Duration()
        {
            var t = new IdleTimer(1);
            t.Begin(1f, 1f);

            Assert.IsFalse(t.IsComplete);
            t.Tick(0.5f);
            Assert.IsFalse(t.IsComplete);
            t.Tick(1f);
            Assert.IsTrue(t.IsComplete);
        }

        [Test]
        public void Duration_Stays_Within_The_Requested_Range()
        {
            var t = new IdleTimer(42);
            for (int i = 0; i < 50; i++)
            {
                t.Begin(2f, 4f);
                Assert.IsFalse(t.IsComplete);
                t.Tick(1.99f);
                Assert.IsFalse(t.IsComplete, "must not finish before the minimum duration");
                t.Tick(2.02f);
                Assert.IsTrue(t.IsComplete, "must finish by the maximum duration");
            }
        }

        [Test]
        public void Reversed_Range_Clamps_To_The_Minimum()
        {
            var t = new IdleTimer(3);
            t.Begin(2f, 1f); // max < min
            t.Tick(2f);
            Assert.IsTrue(t.IsComplete);
        }
    }
}
