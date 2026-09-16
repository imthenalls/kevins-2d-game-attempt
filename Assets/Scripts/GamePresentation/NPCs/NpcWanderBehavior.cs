using Game.Core;
using UnityEngine;

/// <summary>
/// NPC behavior that walks the NPC to a random position within a radius each activation.
/// Uses Rigidbody2D velocity so physics colliders stop it naturally.
/// Casts the NPC's collider shape ahead each frame and abandons targets when the body hits a wall
/// or stalls, preventing the NPC from continually pushing into corners.
///
/// Unity setup:
///   1. Add to an NPC GameObject alongside NpcBehaviorManager.
///   2. Requires a Rigidbody2D: Gravity Scale = 0, freeze Z rotation (shared with NpcBehaviorBase).
///   3. Set Wander Radius, Move Speed, and Arrival Threshold on this component.
///   4. Set Wall Layers to your obstacles layer so the cast detects walls.
///   5. Adjust Wall Look Ahead (~half the NPC's collider radius works well).
///   6. Set Weight (0–100) relative to other behaviors on the same NPC.
/// </summary>
public class NpcWanderBehavior : NpcBehaviorBase
{
    [Header("Config (Game.Data)")]
    [SerializeField] private NpcWanderConfig wanderConfig = new NpcWanderConfig();

    [Header("Unity References")]
    [SerializeField] private LayerMask wallLayers = ~0; // set to your walls layer in Inspector

    private Collider2D[] ownColliders;
    private Vector2 target;

    private static readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];

    protected override void Awake()
    {
        base.Awake();
        ownColliders = GetComponentsInChildren<Collider2D>();
    }

    protected override void Enter()
    {
        target = PickTarget();
    }

    protected override void TickBehavior()
    {
        Vector2 position = Body.position;

        if (Vector2.Distance(position, target) <= wanderConfig.ArrivalThreshold)
        {
            StopMoving();
            Complete();
            return;
        }

        Vector2 direction = (target - position).normalized;

        // Cast the full body shape rather than a zero-width ray from its center.
        // This catches diagonal and edge contacts before physics pins the body to a wall.
        if (HitsWall(position, direction, wanderConfig.WallLookAhead))
        {
            StopMoving();
            Complete();
            return;
        }

        MoveToward(target, Config.MoveSpeed);
    }

    /// <summary>
    /// Tries up to 8 random directions. Returns the first point whose
    /// straight-line path to the NPC is unobstructed, or the origin as fallback.
    /// </summary>
    private Vector2 PickTarget()
    {
        Vector2 origin = Body.position;
        for (int i = 0; i < 8; i++)
        {
            Vector2 candidate = origin + Random.insideUnitCircle * wanderConfig.WanderRadius;
            Vector2 direction = candidate - origin;
            float distance = direction.magnitude;
            if (distance < 0.01f)
                continue;

            if (!HitsWall(origin, direction / distance, distance))
                return candidate;
        }

        // All directions blocked — stay put and complete immediately.
        Complete();
        return origin;
    }

    /// <summary>
    /// Returns true if any collider OTHER than this NPC is hit along the path.
    /// Uses each enabled, solid collider's real shape so the test matches the body footprint.
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
        foreach (Collider2D own in ownColliders)
        {
            if (own == null || !own.enabled || own.isTrigger)
                continue;

            castAnyBodyCollider = true;
            int count = own.Cast(direction, filter, hitBuffer, distance);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hitBuffer[i].collider;
                if (hit != null && !IsOwnCollider(hit))
                    return true;
            }
        }

        // Keep raycast behavior as a safe fallback for an incorrectly configured NPC
        // that has no enabled solid collider.
        if (!castAnyBodyCollider)
        {
            int count = Physics2D.RaycastNonAlloc(origin, direction, hitBuffer, distance, wallLayers);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hitBuffer[i].collider;
                if (hit != null && !hit.isTrigger && !IsOwnCollider(hit))
                    return true;
            }
        }

        return false;
    }

    private bool IsOwnCollider(Collider2D candidate)
    {
        foreach (Collider2D own in ownColliders)
        {
            if (candidate == own)
                return true;
        }

        return false;
    }
}
