using System;

namespace Game.Core
{
    /// <summary>Outcome of a wander tick.</summary>
    public enum WanderDecision
    {
        /// <summary>Still travelling to the target.</summary>
        Moving = 0,

        /// <summary>Reached the target.</summary>
        Arrived = 1,

        /// <summary>No progress for the stall timeout.</summary>
        Stalled = 2,
    }

    /// <summary>
    /// Engine-free wander decisions for an NPC: idle timing, random candidate destination generation,
    /// arrival and stall detection. It holds no Unity types — the Unity adapter supplies geometry
    /// (whether a candidate is blocked, how to move) and applies movement/pathfinding. Plain C#,
    /// lives in Game.Data and is unit-testable without a scene.
    ///
    /// Unity setup: none. Constructed by NpcWander3D / NpcWanderBehavior.
    /// </summary>
    public sealed class WanderModel
    {
        public const int MaxCandidateAttempts = 8;

        // Matches the previous stall epsilon (0.02 world units) squared.
        private const float ProgressEpsilonSqr = 0.0004f;

        private readonly Random rng;
        private readonly float wanderRadius;
        private readonly float arrivalThreshold;
        private readonly float stallTimeout;
        private readonly float idleSeconds;

        private float idleRemaining;
        private bool hasTarget;
        private float targetX, targetZ;
        private int candidateAttempts;
        private float lastX, lastZ, stalledTime;

        public WanderModel(float wanderRadius, float arrivalThreshold, float stallTimeout, float idleSeconds, int seed)
        {
            this.wanderRadius = wanderRadius < 0f ? 0f : wanderRadius;
            this.arrivalThreshold = arrivalThreshold < 0f ? 0f : arrivalThreshold;
            this.stallTimeout = stallTimeout;
            this.idleSeconds = idleSeconds < 0f ? 0f : idleSeconds;
            rng = new Random(seed);
        }

        public bool IsIdle => idleRemaining > 0f;
        public bool HasTarget => hasTarget;
        public float TargetX => targetX;
        public float TargetZ => targetZ;

        /// <summary>Starts an idle pause and clears the current target.</summary>
        public void BeginIdle()
        {
            hasTarget = false;
            candidateAttempts = 0;
            idleRemaining = idleSeconds;
        }

        public void TickIdle(float deltaSeconds)
        {
            if (idleRemaining > 0f && deltaSeconds > 0f)
                idleRemaining = Math.Max(0f, idleRemaining - deltaSeconds);
        }

        /// <summary>
        /// Produces the next random candidate destination on the disc. Returns false once the
        /// attempt budget (8) is spent, so the caller can give up and idle.
        /// </summary>
        public bool TryNextCandidate(float originX, float originZ, out float candidateX, out float candidateZ)
        {
            if (candidateAttempts >= MaxCandidateAttempts)
            {
                candidateX = originX;
                candidateZ = originZ;
                return false;
            }

            candidateAttempts++;
            double angle = rng.NextDouble() * Math.PI * 2.0;
            double radius = Math.Sqrt(rng.NextDouble()) * wanderRadius;
            candidateX = originX + (float)(Math.Cos(angle) * radius);
            candidateZ = originZ + (float)(Math.Sin(angle) * radius);
            return true;
        }

        /// <summary>Accepts a destination passed the caller's obstruction test.</summary>
        public void BeginTarget(float x, float z, float currentX, float currentZ)
        {
            hasTarget = true;
            targetX = x;
            targetZ = z;
            lastX = currentX;
            lastZ = currentZ;
            stalledTime = 0f;
        }

        public void AbortTarget() => hasTarget = false;

        /// <summary>Arrival/stall test. Call while a target is active.</summary>
        public WanderDecision Evaluate(float currentX, float currentZ, float deltaSeconds)
        {
            float dx = targetX - currentX;
            float dz = targetZ - currentZ;
            if (dx * dx + dz * dz <= arrivalThreshold * arrivalThreshold)
                return WanderDecision.Arrived;

            float movedX = currentX - lastX;
            float movedZ = currentZ - lastZ;
            if (movedX * movedX + movedZ * movedZ >= ProgressEpsilonSqr)
            {
                lastX = currentX;
                lastZ = currentZ;
                stalledTime = 0f;
            }
            else
            {
                stalledTime += deltaSeconds;
                if (stalledTime >= stallTimeout)
                    return WanderDecision.Stalled;
            }

            return WanderDecision.Moving;
        }

        /// <summary>Normalized direction from the current position to the target.</summary>
        public bool TryGetDirection(float currentX, float currentZ, out float directionX, out float directionZ)
        {
            float dx = targetX - currentX;
            float dz = targetZ - currentZ;
            float magnitude = (float)Math.Sqrt(dx * dx + dz * dz);
            if (magnitude < 0.0001f)
            {
                directionX = 0f;
                directionZ = 0f;
                return false;
            }

            directionX = dx / magnitude;
            directionZ = dz / magnitude;
            return true;
        }

        public float DistanceToTarget(float currentX, float currentZ)
        {
            float dx = targetX - currentX;
            float dz = targetZ - currentZ;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
