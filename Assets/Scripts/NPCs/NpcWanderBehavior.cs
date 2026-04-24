using UnityEngine;

/// <summary>
/// NPC walks to a random position within wanderRadius.
/// Uses Rigidbody2D.velocity so physics colliders stop it naturally.
/// Raycasts ahead each frame — if a wall is close, gives up immediately
/// rather than grinding into it. Tries several random targets on enter
/// to avoid picking a point that is behind a wall.
/// Requires a Rigidbody2D (Gravity Scale = 0, freeze Z rotation).
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

    public float Weight => weight;

    private Rigidbody2D _rb;
    private Collider2D[] _ownColliders;
    private Vector2 _target;
    private bool _done;

    private static readonly RaycastHit2D[] _hitBuffer = new RaycastHit2D[16];

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _ownColliders = GetComponents<Collider2D>();
    }

    public void OnEnter()
    {
        _done = false;
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

        // Raycast ahead — if a wall is within lookAhead distance, give up now
        if (HitsWall(pos, direction, wallLookAhead))
        {
            Stop();
            return;
        }

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
    /// Returns true if any collider OTHER than this NPC is hit along the ray.
    /// Uses a shared buffer to avoid allocations.
    /// </summary>
    private bool HitsWall(Vector2 origin, Vector2 direction, float distance)
    {
        int count = Physics2D.RaycastNonAlloc(origin, direction, _hitBuffer, distance, wallLayers);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = _hitBuffer[i].collider;
            if (col == null) continue;

            bool isSelf = false;
            foreach (Collider2D own in _ownColliders)
            {
                if (col == own) { isSelf = true; break; }
            }
            if (!isSelf) return true;
        }
        return false;
    }
}
