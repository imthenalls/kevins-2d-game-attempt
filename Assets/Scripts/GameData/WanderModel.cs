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

        /// <summary>No progress and the recovery budget is spent.</summary>
        Stalled = 2,

        /// <summary>No progress; the caller should recompute the route to the target.</summary>
        Repath = 3,
    }

    /// <summary>
    /// Engine-free wander decisions for an NPC: idle timing, random candidate destination generation,
    /// arrival, and stall recovery. It holds no Unity types — the Unity adapter supplies geometry
    /// (whether a candidate is blocked, how to move) and applies movement/pathfinding. Stall/repath
    /// policy is delegated to the shared <see cref="TravelRecoveryModel"/>. Plain C#, lives in
    /// Game.Data and is unit-testable without a scene.
    ///
    /// Unity setup: none. Constructed by NpcWander3D / NpcWanderBehavior.
    /// </summary>
    public sealed class WanderModel
    {
        public const int MaxCandidateAttempts = 8;

        private readonly Random rng;
        private readonly float wanderRadius;
        private readonly float arrivalThreshold;
        private readonly float idleSeconds;
        private readonly float failedTargetRadius;
        private readonly TravelRecoveryModel recovery;

        private float idleRemaining;
        private bool hasTarget;
        private float targetX, targetZ;
        private int candidateAttempts;

        private bool hasFailedTarget;
        private float failedX, failedZ;

        public WanderModel(float wanderRadius, float arrivalThreshold, float stallTimeout, float idleSeconds, int seed)
            : this(wanderRadius, arrivalThreshold, stallTimeout, idleSeconds, seed, 0, 0f)
        {
        }

        public WanderModel(
            float wanderRadius, float arrivalThreshold, float stallTimeout, float idleSeconds, int seed,
            int maxRepaths, float failedTargetRadius)
        {
            this.wanderRadius = wanderRadius < 0f ? 0f : wanderRadius;
            this.arrivalThreshold = arrivalThreshold < 0f ? 0f : arrivalThreshold;
            this.idleSeconds = idleSeconds < 0f ? 0f : idleSeconds;
            this.failedTargetRadius = failedTargetRadius < 0f ? 0f : failedTargetRadius;
            recovery = new TravelRecoveryModel(stallTimeout, maxRepaths);
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
        /// Produces the next random candidate destination on the disc, skipping spots close to the
        /// last dead end. Returns false once the attempt budget (8) is spent, so the caller can give
        /// up and idle.
        /// </summary>
        public bool TryNextCandidate(float originX, float originZ, out float candidateX, out float candidateZ)
        {
            while (candidateAttempts < MaxCandidateAttempts)
            {
                candidateAttempts++;
                double angle = rng.NextDouble() * Math.PI * 2.0;
                double radius = Math.Sqrt(rng.NextDouble()) * wanderRadius;
                candidateX = originX + (float)(Math.Cos(angle) * radius);
                candidateZ = originZ + (float)(Math.Sin(angle) * radius);

                if (hasFailedTarget && failedTargetRadius > 0f)
                {
                    float dx = candidateX - failedX;
                    float dz = candidateZ - failedZ;
                    if (dx * dx + dz * dz <= failedTargetRadius * failedTargetRadius)
                        continue;
                }

                return true;
            }

            candidateX = originX;
            candidateZ = originZ;
            return false;
        }

        /// <summary>Accepts a destination passed the caller's obstruction test.</summary>
        public void BeginTarget(float x, float z, float currentX, float currentZ)
        {
            hasTarget = true;
            targetX = x;
            targetZ = z;
            recovery.Reset(currentX, currentZ);
        }

        /// <summary>Clears the current target without recording it as a dead end.</summary>
        public void AbortTarget() => hasTarget = false;

        /// <summary>
        /// Records the current target as a dead end and clears it, so near-identical candidates are
        /// skipped until a different area is chosen.
        /// </summary>
        public void FailTarget()
        {
            if (hasTarget && failedTargetRadius > 0f)
            {
                failedX = targetX;
                failedZ = targetZ;
                hasFailedTarget = true;
            }

            hasTarget = false;
        }

        /// <summary>Arrival/recovery test. Call while a target is active.</summary>
        public WanderDecision Evaluate(float currentX, float currentZ, float deltaSeconds)
        {
            float dx = targetX - currentX;
            float dz = targetZ - currentZ;
            if (dx * dx + dz * dz <= arrivalThreshold * arrivalThreshold)
                return WanderDecision.Arrived;

            TravelRecoveryDecision recoveryDecision =
                recovery.Evaluate(currentX, currentZ, deltaSeconds, atWaypoint: false);

            switch (recoveryDecision)
            {
                case TravelRecoveryDecision.Repath:
                    return WanderDecision.Repath;
                case TravelRecoveryDecision.Abandon:
                    return WanderDecision.Stalled;
                default:
                    return WanderDecision.Moving;
            }
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
