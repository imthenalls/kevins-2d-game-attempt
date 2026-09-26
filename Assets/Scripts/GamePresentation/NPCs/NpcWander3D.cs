using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Planar-isometric NPC wanderer for 3D scenes. Thin facade over the engine-free
/// <see cref="WanderModel"/>: this component only samples geometry (blocked candidates, walls ahead)
/// and applies velocity; idle timing, candidate generation, arrival and stall/repath recovery live
/// in Game.Data and are unit-tested there.
///
/// Nearby NPCs are steered around via <see cref="NpcLocalAvoidance"/> (not treated as walls), so
/// wanderers slide past one another instead of deadlocking. When a destination is not in a straight
/// line, an attached <see cref="NpcPathfinder3D"/> routes around buildings; if no route exists the
/// target is abandoned and a different spot is chosen.
///
/// Unity setup:
///   1. Add to an NPC root with a Rigidbody (Use Gravity off) and a CapsuleCollider.
///   2. Add NpcController for identity (id, type, dialogue).
///   3. Put the sprite on a child with BillboardSprite.
///   4. Set Wall Layers to the Walls layer (buildings and perimeter walls).
///   5. Optionally add NpcPathfinder3D and set Neighbor Layers to the Npc layer.
///   6. Tune Wander Radius / Idle Seconds, and the Travel Recovery config.
///
/// Runtime API: none.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[DisallowMultipleComponent]
public class NpcWander3D : MonoBehaviour
{
    [Header("Config (Game.Data)")]
    [SerializeField] private NpcBehaviorConfig behaviorConfig = new NpcBehaviorConfig();
    [SerializeField] private NpcWanderConfig wanderConfig = new NpcWanderConfig();
    [SerializeField] private NpcTravelRecoveryConfig recoveryConfig = new NpcTravelRecoveryConfig();

    [Header("Unity References")]
    [Tooltip("Layers that bound the wander area (buildings, perimeter walls).")]
    [SerializeField] private LayerMask wallLayers = ~0;
    [Tooltip("Layers whose members this NPC steers around so wanderers can pass, not deadlock.")]
    [SerializeField] private LayerMask neighborLayers = 0;
    [Tooltip("Distance at which nearby NPCs start pushing this one aside.")]
    [SerializeField, Min(0f)] private float neighborSeparation = 0.9f;
    [Tooltip("How strongly nearby NPCs deflect movement (0 = ignore neighbors).")]
    [SerializeField, Min(0f)] private float neighborSteerStrength = 1.5f;
    [Tooltip("Route around buildings with NpcPathfinder3D when the target is not in a straight line.")]
    [SerializeField] private bool usePathfinding = true;
    [Tooltip("Log stall/repath decisions. Development only.")]
    [SerializeField] private bool logDiagnostics = false;

    private const float WaypointReached = 0.35f;
    private const float FailedTargetRadius = 1f;

    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private NpcController controller;
    private NpcPathfinder3D pathfinder;
    private WanderModel model;
    private List<Vector3> path;
    private int pathIndex;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        controller = GetComponent<NpcController>();
        pathfinder = GetComponent<NpcPathfinder3D>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        model = new WanderModel(
            wanderConfig.WanderRadius, wanderConfig.ArrivalThreshold,
            recoveryConfig.StallTimeout, wanderConfig.IdleSeconds,
            Random.Range(1, int.MaxValue), recoveryConfig.MaxRepaths, FailedTargetRadius);
        model.BeginIdle();
    }

    private void Update()
    {
        // Awake normally builds the model; guard so a component added before its dependencies
        // (or on an object whose Awake was skipped) cannot spam exceptions every frame.
        if (model == null)
        {
            Stop();
            return;
        }

        // Hold still while talking (dialogue, cutscene) or otherwise not idle.
        if (controller != null && controller.BehaviorState != NpcBehaviorState.Idle)
        {
            Stop();
            return;
        }

        if (model.IsIdle)
        {
            Stop();
            model.TickIdle(Time.deltaTime);
            return;
        }

        if (!model.HasTarget)
        {
            if (!TryAcquireTarget())
            {
                model.BeginIdle();
                Stop();
                return;
            }
        }

        Vector3 position = body.position;
        WanderDecision decision = model.Evaluate(position.x, position.z, Time.deltaTime);

        if (decision == WanderDecision.Repath)
        {
            if (!TryRebuildPath())
            {
                LogDiagnostic("repath failed, abandoning target");
                FailTarget();
                return;
            }

            LogDiagnostic("repath OK");
            decision = WanderDecision.Moving;
        }

        if (decision != WanderDecision.Moving)
        {
            if (decision == WanderDecision.Stalled)
            {
                LogDiagnostic("stalled, abandoning target");
                FailTarget();
            }
            else
            {
                model.AbortTarget();
                model.BeginIdle();
                Stop();
            }

            return;
        }

        Move(position);
    }

    // Drives toward the current waypoint, or the target when no route is active. Nearby NPCs are
    // steered around; the static world blocks movement and lets recovery repath.
    private void Move(Vector3 position)
    {
        Vector3 waypoint;
        bool onPath = path != null && pathIndex < path.Count;

        if (onPath)
        {
            waypoint = path[pathIndex];
        }
        else
        {
            if (path != null)
            {
                path = null; // exhausted; fall back to the target itself
                pathIndex = 0;
            }

            waypoint = new Vector3(model.TargetX, position.y, model.TargetZ);
        }

        Vector3 delta = waypoint - position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 0.0001f)
        {
            Stop();
            return;
        }

        Vector3 direction = delta / distance;

        Vector3 separation = NpcLocalAvoidance.Compute(
            position, body, bodyCollider, neighborLayers, neighborSeparation, gameObject.name.GetHashCode());
        direction = NpcLocalAvoidance.Steer(direction, separation, neighborSteerStrength);

        if (HitsWall(direction, distance))
        {
            // Blocked by the static world. Stand still; the recovery model will ask for a repath
            // (or abandon the target) after the stall timeout.
            Stop();
            return;
        }

        body.linearVelocity = direction * behaviorConfig.MoveSpeed;
    }

    private void FailTarget()
    {
        path = null;
        model.FailTarget();
        model.BeginIdle();
        Stop();
    }

    // Tries up to the model's candidate budget. A candidate is accepted when the straight line to it
    // is clear, or when a path around the world can be found to it.
    private bool TryAcquireTarget()
    {
        Vector3 origin = body.position;
        while (model.TryNextCandidate(origin.x, origin.z, out float candidateX, out float candidateZ))
        {
            Vector3 candidate = new Vector3(candidateX, origin.y, candidateZ);
            Vector3 delta = candidate - origin;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 0.01f)
                continue;

            Vector3 direction = delta / distance;
            if (!HitsWall(direction, distance))
            {
                model.BeginTarget(candidateX, candidateZ, origin.x, origin.z);
                path = null;
                pathIndex = 0;
                return true;
            }

            if (usePathfinding && pathfinder != null)
            {
                List<Vector3> routed = pathfinder.FindPath(origin, candidate);
                if (routed != null && routed.Count > 0)
                {
                    model.BeginTarget(candidateX, candidateZ, origin.x, origin.z);
                    path = routed;
                    pathIndex = 0;
                    return true;
                }
            }
        }

        return false;
    }

    // Recomputes the route to the current target. Returns false when no route exists.
    private bool TryRebuildPath()
    {
        if (!usePathfinding || pathfinder == null || !model.HasTarget)
            return false;

        List<Vector3> routed = pathfinder.FindPath(
            body.position, new Vector3(model.TargetX, body.position.y, model.TargetZ));
        if (routed == null || routed.Count == 0)
            return false;

        path = routed;
        pathIndex = 0;
        return true;
    }

    private void Stop()
    {
        if (body != null)
            body.linearVelocity = Vector3.zero;
    }

    // Sphere-casts the body ahead so it stops before pushing into a wall or building. Neighbors are
    // intentionally not blockers here; they are steered around in NpcLocalAvoidance.
    private bool HitsWall(Vector3 direction, float distance)
    {
        float radius = bodyCollider != null ? bodyCollider.radius : 0.3f;
        Vector3 origin = body.position + Vector3.up * (bodyCollider != null ? bodyCollider.center.y : 0.7f) + direction * 0.05f;

        int count = Physics.SphereCastNonAlloc(
            origin, radius, direction, HitBuffer, distance, wallLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = HitBuffer[i].collider;
            if (hit != null && hit != bodyCollider && !hit.isTrigger)
                return true;
        }

        return false;
    }

    private void LogDiagnostic(string message)
    {
        if (!logDiagnostics)
            return;

        Vector3 target = new Vector3(model.TargetX, 0f, model.TargetZ);
        Debug.Log($"[NpcWander3D] '{name}' {message}. pos={body.position} target={target} " +
                  $"waypoint={pathIndex}/{ (path != null ? path.Count : 0) }", this);
    }
}
