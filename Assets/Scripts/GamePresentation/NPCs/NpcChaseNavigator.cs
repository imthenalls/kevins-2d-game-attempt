using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Steers an enemy around obstacles toward a moving target. Wraps <see cref="NpcPathfinder3D"/>
/// with a repath cadence and waypoint following so chase AIs do not press straight into walls.
/// The engine-free <see cref="NpcChaseNavigationPolicy"/> owns the decision to drive direct, repath,
/// follow the committed route, or wait; the adapter only raycasts, generates paths, and converts
/// vectors.
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
    [Header("Config (Game.Data)")]
    [SerializeField] private NpcChaseNavigationConfig config = new NpcChaseNavigationConfig();

    private NpcPathfinder3D pathfinder;
    private NpcChaseNavigationPolicy policy;
    private readonly RouteFollower route = new RouteFollower();

    private void Awake()
    {
        pathfinder = GetComponent<NpcPathfinder3D>();
        policy = new NpcChaseNavigationPolicy(config);
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

        // The Core policy decides direct-vs-repath from the line-of-sight result and its cadence.
        NpcChaseNavigationDecision decision = policy.Evaluate(
            Time.time, self.x, self.z, target.x, target.z, HasClearLine(self, target), repathInterval);

        if (decision.DriveDirect)
        {
            route.Clear();
            return flat / distance;
        }

        if (decision.Repath)
        {
            route.Clear();

            // Path generation and Unity vector conversion stay in the adapter; the decision of what
            // to do with the result is reported back to Core.
            List<Vector3> found = pathfinder.FindPath(self, target);
            bool hasPath = found != null && found.Count > 0;
            if (hasPath)
                route.SetRoute(NpcRoute.FromXZ(found));

            decision = policy.ReportPathResult(hasPath);
        }

        if (decision.Wait)
            return Vector3.zero;

        route.Advance(self.x, self.z, config.WaypointReached);
        if (route.TryCurrent(out float waypointX, out float waypointZ))
        {
            Vector3 toWaypoint = new Vector3(waypointX - self.x, 0f, waypointZ - self.z);
            if (toWaypoint.sqrMagnitude > 0.0001f)
                return toWaypoint.normalized;
        }

        // The route was consumed while the target is still blocked. Report it so Core repaths at the
        // next cadence; never invent a direct step that walks straight into the obstacle.
        policy.ReportRouteExhausted();
        return Vector3.zero;
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
