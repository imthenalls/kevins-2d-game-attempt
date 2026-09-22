using Game.Core;
using UnityEngine;

/// <summary>
/// Planar-isometric NPC wanderer for 3D scenes. Picks a random point within the wander radius on the
/// XZ plane, walks to it, and repeats — avoiding walls with a sphere cast. This is the 3D counterpart
/// of <c>NpcWanderBehavior</c> (which is Physics2D); it deliberately avoids grid pathfinding so it
/// needs no tilemap. Reuses the authoritative Game.Data tuning configs.
///
/// Unity setup:
///   1. Add to an NPC root with a Rigidbody (Use Gravity off) and a CapsuleCollider.
///   2. Add NpcController for identity (id, type, dialogue).
///   3. Put the sprite on a child with BillboardSprite.
///   4. Set Wall Layers to the Walls layer (buildings and perimeter walls).
///   5. Set Wander Radius on the nested Wander Config, and Move Speed on the Behavior Config.
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
    [Tooltip("Layers that block movement (buildings, walls).")]
    [SerializeField] private LayerMask wallLayers = ~0;

    [Tooltip("Pause between destinations, in seconds.")]
    [SerializeField, Min(0f)] private float idleSeconds = 1.5f;

    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private Vector3 target;
    private bool hasTarget;
    private float idleRemaining;
    private Vector3 lastPosition;
    private float stalledTime;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<CapsuleCollider>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        idleRemaining = Random.Range(0f, idleSeconds);
        lastPosition = body.position;
    }

    private void Update()
    {
        if (idleRemaining > 0f)
        {
            Stop();
            idleRemaining -= Time.deltaTime;
            return;
        }

        if (!hasTarget)
        {
            if (!TryPickTarget(out target))
            {
                Stop();
                idleRemaining = idleSeconds;
                return;
            }

            hasTarget = true;
            lastPosition = body.position;
            stalledTime = 0f;
        }

        Vector3 delta = target - body.position;
        delta.y = 0f;

        if (delta.magnitude <= wanderConfig.ArrivalThreshold)
        {
            Arrive();
            return;
        }

        Vector3 direction = delta.normalized;
        if (HitsWall(direction, delta.magnitude))
        {
            Arrive();
            return;
        }

        body.linearVelocity = direction * behaviorConfig.MoveSpeed;

        if ((body.position - lastPosition).sqrMagnitude >= 0.0004f)
        {
            lastPosition = body.position;
            stalledTime = 0f;
        }
        else
        {
            stalledTime += Time.deltaTime;
            if (stalledTime >= behaviorConfig.StallTimeout)
                Arrive();
        }
    }

    private void Arrive()
    {
        hasTarget = false;
        Stop();
        idleRemaining = idleSeconds;
    }

    private void Stop()
    {
        if (body != null)
            body.linearVelocity = Vector3.zero;
    }

    // Tries random directions within the wander radius, returning the first unobstructed target.
    private bool TryPickTarget(out Vector3 result)
    {
        Vector3 origin = body.position;
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderConfig.WanderRadius;
            Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);
            Vector3 direction = candidate - origin;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance < 0.01f)
                continue;

            if (!HitsWall(direction / distance, distance))
            {
                result = candidate;
                return true;
            }
        }

        result = origin;
        return false;
    }

    // Sphere-casts the body ahead so it stops before pushing into a wall or building.
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
}
