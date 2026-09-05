using UnityEngine;

/// <summary>
/// NPC behavior that walks the NPC to a random position within a radius each activation.
/// Uses Rigidbody2D velocity so physics colliders stop it naturally.
/// Casts the NPC's collider shape ahead each frame and abandons targets when movement stalls,
/// preventing the NPC from continually pushing into walls or corners.
/// Tries up to 8 random target positions on enter to find an unobstructed path.
/// Flips the SpriteRenderer horizontally to face the direction of travel.
///
/// Unity setup:
///   1. Add to an NPC GameObject alongside NpcBehaviorManager.
///   2. Requires a Rigidbody2D: set Gravity Scale = 0, freeze Z rotation.
///   3. Set Wander Radius (world units), Move Speed, and Arrival Threshold.
///   4. Set Wall Layers to your obstacles layer so the raycast detects walls.
///   5. Adjust Wall Look Ahead (~half the NPC's collider radius works well).
///   6. Set Weight (0–100) relative to other behaviors on the same NPC.
///   7. Sprite Renderer is auto-detected (self then children); assign manually if needed.
///      flipX = false → facing right (default). flipX = true → facing left.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class NpcWanderBehavior : MonoBehaviour, INpcBehavior
{
    [SerializeField, Range(0f, 100f)] private float weight = 50f;
    [SerializeField] private float wanderRadius = 3f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float arrivalThreshold = 0.2f;
    /// <summary>How far ahead to raycast for walls. ~half the NPC width works well.</summary>
    [SerializeField] private float wallLookAhead = 0.3f;
    [SerializeField] private LayerMask wallLayers = ~0; // set to your walls layer in Inspector
    [SerializeField] private SpriteRenderer spriteRenderer;

    public float Weight => weight;

    private Rigidbody2D _rb;
    private Collider2D[] _ownColliders;
    private Vector2 _target;
    private bool _done;
    private Vector2 _lastProgressPosition;
    private float _stalledTime;

    private static readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];
    private const float ProgressDistance = 0.01f;
    private const float StalledTimeout = 0.5f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _ownColliders = GetComponentsInChildren<Collider2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void OnEnter()
    {
        _done = false;
        _stalledTime = 0f;
        _lastProgressPosition = _rb.position;
        _target = PickTarget();
    }

    public void Tick()
    {
        Vector2 pos = _rb.position;
        Vector2 toTarget = _target - pos;
        float distance = toTarget.magnitude;

        if (distance <= arrivalThreshold)
        {
            Stop();
            return;
        }

        Vector2 direction = toTarget / distance;

        // Cast the full body shape rather than a zero-width ray from its center.
        // This catches diagonal and edge contacts before physics pins the body to a wall.
        if (HitsWall(pos, direction, wallLookAhead))
        {
            Stop();
            return;
        }

        if (HasStalled(pos))
        {
            Stop();
            return;
        }

        if (spriteRenderer != null)
            spriteRenderer.flipX = direction.x < 0f;

        _rb.linearVelocity = direction * moveSpeed;
    }

    public void OnExit() => Stop();

    public bool IsComplete() => _done;

    // ----------------------------------------------------------------

    private void Stop()
    {
        _rb.linearVelocity = Vector2.zero;
        _done = true;
    }

    private bool HasStalled(Vector2 position)
    {
        if ((position - _lastProgressPosition).sqrMagnitude >= ProgressDistance * ProgressDistance)
        {
            _lastProgressPosition = position;
            _stalledTime = 0f;
            return false;
        }

        _stalledTime += Time.deltaTime;
        return _stalledTime >= StalledTimeout;
    }

    /// <summary>
    /// Tries up to 8 random directions. Returns the first point whose
    /// straight-line path to the NPC is unobstructed, or the origin as fallback.
    /// </summary>
    private Vector2 PickTarget()
    {
        Vector2 origin = _rb.position;
        for (int i = 0; i < 8; i++)
        {
            Vector2 candidate = origin + Random.insideUnitCircle * wanderRadius;
            Vector2 direction = candidate - origin;
            float dist = direction.magnitude;
            if (dist < 0.01f) continue;

            if (!HitsWall(origin, direction / dist, dist))
                return candidate;
        }

        // All directions blocked — stay put and complete immediately
        _done = true;
        return origin;
    }

    /// <summary>
    /// Returns true if any collider OTHER than this NPC is hit along the path.
    /// Uses each enabled, solid collider's real shape so the test matches the body footprint.
    /// Uses a shared buffer to avoid allocations.
    /// </summary>
    private bool HitsWall(Vector2 origin, Vector2 direction, float distance)
    {
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = wallLayers,
            useTriggers = false
        };

        bool castAnyBodyCollider = false;
        foreach (Collider2D own in _ownColliders)
        {
            if (own == null || !own.enabled || own.isTrigger)
                continue;

            castAnyBodyCollider = true;
            int count = own.Cast(direction, filter, _hitBuffer, distance);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hitBuffer[i].collider;
                if (hit != null && !IsOwnCollider(hit))
                    return true;
            }
        }

        // Keep raycast behavior as a safe fallback for an incorrectly configured NPC
        // that has no enabled solid collider.
        if (!castAnyBodyCollider)
        {
            int count = Physics2D.RaycastNonAlloc(origin, direction, _hitBuffer, distance, wallLayers);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hitBuffer[i].collider;
                if (hit != null && !hit.isTrigger && !IsOwnCollider(hit))
                    return true;
            }
        }

        return false;
    }

    private bool IsOwnCollider(Collider2D candidate)
    {
        foreach (Collider2D own in _ownColliders)
        {
            if (candidate == own)
                return true;
        }

        return false;
    }
}
