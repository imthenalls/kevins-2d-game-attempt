using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free "stand still for a random duration" timer. Holds a seeded RNG and the remaining
    /// time, so the idle rule is unit-testable without a scene. Plain C#, lives in Game.Data.
    ///
    /// Unity setup: none. Constructed by NpcIdleBehavior.
    /// </summary>
    public sealed class IdleTimer
    {
        private readonly Random rng;
        private float remaining;

        public IdleTimer(int seed)
        {
            rng = new Random(seed);
        }

        public bool IsComplete => remaining <= 0f;

        /// <summary>Begins an idle of a random duration in [minSeconds, maxSeconds].</summary>
        public void Begin(float minSeconds, float maxSeconds)
        {
            if (maxSeconds < minSeconds)
                maxSeconds = minSeconds;

            remaining = minSeconds + (float)(rng.NextDouble() * (maxSeconds - minSeconds));
        }

        public void Tick(float deltaSeconds)
        {
            if (remaining > 0f && deltaSeconds > 0f)
                remaining = Math.Max(0f, remaining - deltaSeconds);
        }
    }
}
