using Game.Core;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NPC behavior that walks to a random position within a radius each activation. Thin facade over
/// the engine-free <see cref="WanderModel"/>: this component only samples geometry (candidate
/// obstruction, walls ahead, grid paths) and applies velocity; target/idle/arrival/stall decisions
/// live in Game.Data and are unit-tested there.
///
/// Unity setup:
///   1. Add to an NPC GameObject alongside NpcBehaviorManager.
///   2. Requires a Rigidbody2D: Gravity Scale = 0, freeze Z rotation (shared with NpcBehaviorBase).
///   3. Set Wander Radius / Arrival Threshold / Wall Look Ahead on the nested Wander Config.
///   4. Set Wall Layers to your obstacles layer so the cast detects walls.
///   5. Set Weight (0–100) relative to other behaviors on the same NPC.
/// </summary>
public class NpcWanderBehavior : NpcBehaviorBase
{
    [Header("Config (Game.Data)")]
    [SerializeField] private NpcWanderConfig wanderConfig = new NpcWanderConfig();

    [Header("Unity References")]
    [SerializeField] private LayerMask wallLayers = ~0; // set to your walls layer in Inspector

    private Collider2D[] ownColliders;
    private WanderModel model;
    private List<Vector2> path;
    private int pathIndex;

    private static readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];

    protected override void Awake()
    {
        base.Awake();
        ownColliders = GetComponentsInChildren<Collider2D>();
        model = new WanderModel(
            wanderConfig.WanderRadius, wanderConfig.ArrivalThreshold,
            Config.StallTimeout, 0f, Random.Range(1, int.MaxValue));
    }

    protected override void Enter()
    {
        AcquireTarget();
        BuildPath();
    }

    /// <summary>True when the behavior is currently following a computed path.</summary>
    public bool HasPath => path != null && path.Count > 0;

    /// <summary>
    /// Re-selects a destination and rebuilds the path without waiting for a new activation. Used by
    /// the performance harness to force a synchronized re-path across many NPCs in one frame.
    /// </summary>
    public void Repath()
    {
        AcquireTarget();
        BuildPath();
    }

    protected override void TickBehavior()
    {
        if (path != null)
        {
            FollowPath();
            return;
        }

        Vector2 position = Body.position;

        if (!model.HasTarget)
        {
            StopMoving();
            Complete();
            return;
        }

        if (model.Evaluate(position.x, position.y, Time.deltaTime) != WanderDecision.Moving)
        {
            StopMoving();
            Complete();
            return;
        }

        if (!model.TryGetDirection(position.x, position.y, out float dirX, out float dirY))
        {
            StopMoving();
            Complete();
            return;
        }

        Vector2 direction = new Vector2(dirX, dirY);

        // Cast the full body shape rather than a zero-width ray from its center.
        // This catches diagonal and edge contacts before physics pins the body to a wall.
        if (HitsWall(position, direction, wanderConfig.WallLookAhead))
        {
            StopMoving();
            Complete();
            return;
        }

        MoveToward(new Vector2(model.TargetX, model.TargetZ), Config.MoveSpeed);
    }

    // Tries up to the model's candidate budget, accepting the first unobstructed destination.
    private void AcquireTarget()
    {
        Vector2 origin = Body.position;
        while (model.TryNextCandidate(origin.x, origin.y, out float candidateX, out float candidateY))
        {
            Vector2 candidate = new Vector2(candidateX, candidateY);
            Vector2 direction = candidate - origin;
            float distance = direction.magnitude;
            if (distance < 0.01f)
                continue;

            if (!HitsWall(origin, direction / distance, distance))
            {
                model.BeginTarget(candidateX, candidateY, origin.x, origin.y);
                return;
            }
        }
    }

    /// <summary>Builds a grid path to the current target when pathfinding is enabled.</summary>
    private void BuildPath()
    {
        path = null;
        pathIndex = 0;

        if (!wanderConfig.UsePathfinding || Pathfinder == null || Body == null || !model.HasTarget)
            return;

        List<Vector2> computed = Pathfinder.FindPath(Body.position, new Vector2(model.TargetX, model.TargetZ));
        if (computed != null && computed.Count > 0)
            path = computed;
    }

    /// <summary>Follows the current path waypoint by waypoint; straight-line is the fallback.</summary>
    private void FollowPath()
    {
        if (pathIndex >= path.Count)
        {
            StopMoving();
            Complete();
            return;
        }

        Vector2 waypoint = path[pathIndex];
        if (Arrived(waypoint, wanderConfig.ArrivalThreshold))
        {
            pathIndex++;
            if (pathIndex >= path.Count)
            {
                StopMoving();
                Complete();
            }
            return;
        }

        MoveToward(waypoint, Config.MoveSpeed);
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
