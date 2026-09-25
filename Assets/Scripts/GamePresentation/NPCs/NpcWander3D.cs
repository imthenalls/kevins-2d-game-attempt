using Game.Core;
using UnityEngine;

/// <summary>
/// Planar-isometric NPC wanderer for 3D scenes. Thin facade over the engine-free
/// <see cref="WanderModel"/>: this component only samples geometry (blocked candidates, walls ahead)
/// and applies velocity; idle timing, candidate generation, arrival and stall decisions live in
/// Game.Data and are unit-tested there.
///
/// Unity setup:
///   1. Add to an NPC root with a Rigidbody (Use Gravity off) and a CapsuleCollider.
///   2. Add NpcController for identity (id, type, dialogue).
///   3. Put the sprite on a child with BillboardSprite.
///   4. Set Wall Layers to the Walls layer (buildings and perimeter walls).
///   5. Tune Wander Radius / Idle Seconds on the nested Wander Config and Move Speed on the Behavior Config.
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

    [Header("Unity References")]
    [Tooltip("Layers that bound the wander area (buildings, perimeter walls).")]
    [SerializeField] private LayerMask wallLayers = ~0;
    [Tooltip("If set, other members on these layers also block movement so NPCs do not stack.")]
    [SerializeField] private LayerMask neighborLayers = 0;

    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private NpcController controller;
    private WanderModel model;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        controller = GetComponent<NpcController>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        model = new WanderModel(
            wanderConfig.WanderRadius, wanderConfig.ArrivalThreshold,
            behaviorConfig.StallTimeout, wanderConfig.IdleSeconds,
            Random.Range(1, int.MaxValue));
        model.BeginIdle();
    }

    private void Update()
    {
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
        if (decision != WanderDecision.Moving)
        {
            model.AbortTarget();
            model.BeginIdle();
            Stop();
            return;
        }

        if (!model.TryGetDirection(position.x, position.z, out float dirX, out float dirZ))
        {
            model.AbortTarget();
            model.BeginIdle();
            Stop();
            return;
        }

        Vector3 direction = new Vector3(dirX, 0f, dirZ);
        if (HitsWall(direction, model.DistanceToTarget(position.x, position.z)))
        {
            model.AbortTarget();
            model.BeginIdle();
            Stop();
            return;
        }

        body.linearVelocity = direction * behaviorConfig.MoveSpeed;
    }

    // Tries up to the model's candidate budget, accepting the first unobstructed destination.
    private bool TryAcquireTarget()
    {
        Vector3 origin = body.position;
        while (model.TryNextCandidate(origin.x, origin.z, out float candidateX, out float candidateZ))
        {
            float dx = candidateX - origin.x;
            float dz = candidateZ - origin.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance < 0.01f)
                continue;

            if (!HitsWall(new Vector3(dx / distance, 0f, dz / distance), distance))
            {
                model.BeginTarget(candidateX, candidateZ, origin.x, origin.z);
                return true;
            }
        }

        return false;
    }

    private void Stop()
    {
        if (body != null)
            body.linearVelocity = Vector3.zero;
    }

    // Sphere-casts the body ahead so it stops before pushing into a wall, building, or neighbor NPC.
    private bool HitsWall(Vector3 direction, float distance)
    {
        float radius = bodyCollider != null ? bodyCollider.radius : 0.3f;
        Vector3 origin = body.position + Vector3.up * (bodyCollider != null ? bodyCollider.center.y : 0.7f) + direction * 0.05f;

        int blockers = wallLayers | neighborLayers;
        int count = Physics.SphereCastNonAlloc(
            origin, radius, direction, HitBuffer, distance, blockers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = HitBuffer[i].collider;
            if (hit != null && hit != bodyCollider && !hit.isTrigger)
                return true;
        }

        return false;
    }
}
