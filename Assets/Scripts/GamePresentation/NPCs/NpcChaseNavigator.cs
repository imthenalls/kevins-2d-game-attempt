using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Steers an enemy around obstacles toward a moving target. Wraps <see cref="NpcPathfinder3D"/>
/// with a repath cadence and waypoint following so chase AIs do not press straight into walls.
/// Falls back to a direct step when no path is found (or the target is in line of sight).
///
/// Unity setup:
///   1. Add to an enemy root that already has NpcPathfinder3D (obstacleLayers set to Walls).
///   2. Chase AIs call <see cref="TryGetStepDirection"/> each movement tick instead of aiming directly.
///
/// Runtime API: TryGetStepDirection(target, repathInterval) returns a normalized XZ direction.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcPathfinder3D))]
public sealed class NpcChaseNavigator : MonoBehaviour
{
    private const float WaypointReached = 0.35f;
    private const float RepathTargetMoved = 1.5f;

    [Tooltip("Stop and move directly at the target once within this distance (line-of-sight melee).")]
    [SerializeField, Min(0.1f)] private float directRange = 2.5f;

    [Tooltip("Skip pathfinding beyond this distance and step directly; chase should be local.")]
    [SerializeField, Min(1f)] private float maxPathDistance = 25f;

    private NpcPathfinder3D pathfinder;
    private readonly List<Vector3> path = new List<Vector3>();
    private int pathIndex;
    private float nextRepathAt;
    private Vector3 lastPathGoal;

    private void Awake()
    {
        pathfinder = GetComponent<NpcPathfinder3D>();
    }

    /// <summary>
    /// Returns a normalized world-space XZ direction the enemy should move to close on
    /// <paramref name="target"/>, routing around obstacles. Returns zero when no step is needed.
    /// </summary>
    public Vector3 TryGetStepDirection(Vector3 target, Vector3 self, float repathInterval)
    {
        Vector3 flat = target - self;
        flat.y = 0f;
        float distance = flat.magnitude;
        if (distance <= 0.001f)
            return Vector3.zero;

        // Close enough and unobstructed: drive straight in so melee stays responsive.
        if (distance <= directRange || distance > maxPathDistance || HasClearLine(self, target))
        {
            path.Clear();
            pathIndex = 0;
            return flat / distance;
        }

        if (Time.time >= nextRepathAt || (target - lastPathGoal).sqrMagnitude > RepathTargetMoved * RepathTargetMoved)
        {
            nextRepathAt = Time.time + Mathf.Max(0.05f, repathInterval);
            lastPathGoal = target;
            pathIndex = 0;
            path.Clear();

            List<Vector3> found = pathfinder.FindPath(self, target);
            if (found != null && found.Count > 0)
                path.AddRange(found);
        }

        while (pathIndex < path.Count)
        {
            Vector3 toWaypoint = path[pathIndex] - self;
            toWaypoint.y = 0f;
            if (toWaypoint.magnitude > WaypointReached)
                return toWaypoint.normalized;
            pathIndex++;
        }

        // No usable path: fall back to a direct step so the enemy at least tries.
        return flat / distance;
    }

    private bool HasClearLine(Vector3 self, Vector3 target)
    {
        Vector3 flat = target - self;
        flat.y = 0f;
        float distance = flat.magnitude;
        if (distance <= 0.001f)
            return true;

        Vector3 origin = self + Vector3.up * 0.9f;
        Vector3 direction = flat / distance;

        // Sphere-cast with the enemy's body width so a wall corner that would clip the collider
        // (but not a thin center ray) is treated as blocked and the enemy routes around it.
        float radius = 0.4f;
        CapsuleCollider capsule = GetComponentInParent<CapsuleCollider>();
        if (capsule != null)
            radius = Mathf.Max(0.05f, capsule.radius * 0.9f);

        RaycastHit[] hits = new RaycastHit[8];
        int count = Physics.SphereCastNonAlloc(
            origin, radius, direction, hits, distance, pathfinder.ObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider != null && hits[i].collider.gameObject != gameObject)
                return false;
        }

        return true;
    }
}
