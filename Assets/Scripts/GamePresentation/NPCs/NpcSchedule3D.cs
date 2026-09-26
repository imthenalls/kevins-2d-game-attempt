using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Gives a 3D NPC a daily loop: wander for a while, then walk to a specific house's door and go
/// inside (via the portal system), stay a while, and come back out. The NPC "owns" a home door and
/// carries a key id; the door stays open while the owner is home.
///
/// The phase and remaining time are authoritative in the pure-C# <see cref="NpcScheduleState"/>
/// (Game.Data) owned by GameSession — this component is a thin facade that reads the model, performs
/// the movement/teleport, and issues model commands. Stall handling delegates to the shared
/// <see cref="TravelRecoveryModel"/>: a blocked NPC pauses, recomputes its route a bounded number of
/// times, then cancels the trip and resumes wandering. Neighbors are steered around with
/// <see cref="NpcLocalAvoidance"/>. Tuning is in <see cref="NpcScheduleConfig"/> and
/// <see cref="NpcTravelRecoveryConfig"/>. Requires <see cref="NpcPathfinder3D"/> to route to the door.
///
/// Unity setup:
///   1. Add to an NPC root that already has Rigidbody, CapsuleCollider, NpcWander3D,
///      NpcPathfinder3D, NpcController and NpcStateView.
///   2. Set Home Door Portal Id (the town door, e.g. "door_4_14"), Home Interior Portal Id
///      (the room portal, e.g. "int_4_14"), and Home Entrance (the door's approach transform).
///   3. Set Away/Home seconds on the nested Schedule config, and Neighbor Layers to the Npc layer.
///
/// Runtime API: none.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class NpcSchedule3D : MonoBehaviour
{
    [Header("Home")]
    [Tooltip("Portal id of the town door this NPC enters (e.g. door_4_14).")]
    [SerializeField] private string homeDoorPortalId;
    [Tooltip("Portal id of the interior portal it arrives at (e.g. int_4_14).")]
    [SerializeField] private string homeInteriorPortalId;
    [Tooltip("Point just outside the door the NPC walks to before going in.")]
    [SerializeField] private Transform homeEntrance;
    [Tooltip("Key the NPC carries for its home (its inventory key).")]
    [SerializeField] private string homeKeyId = "house_key";

    [Header("Config (Game.Data)")]
    [SerializeField] private NpcBehaviorConfig movementConfig = new NpcBehaviorConfig();
    [SerializeField] private NpcScheduleConfig scheduleConfig = new NpcScheduleConfig();
    [SerializeField] private NpcTravelRecoveryConfig recoveryConfig = new NpcTravelRecoveryConfig();

    [Header("Local Avoidance (Unity)")]
    [Tooltip("Layers whose members this NPC steers around while commuting.")]
    [SerializeField] private LayerMask neighborLayers = 0;
    [SerializeField, Min(0f)] private float neighborSeparation = 0.9f;
    [SerializeField, Min(0f)] private float neighborSteerStrength = 1.2f;
    [Tooltip("Log schedule/route decisions. Development only.")]
    [SerializeField] private bool logDiagnostics = false;
    [Tooltip("Distance to the home approach at which the NPC goes inside, without needing the exact point.")]
    [SerializeField, Min(0.1f)] private float homeDoorEnterRadius = 1.5f;

    private const float WaypointReached = 0.35f;

    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private NpcPathfinder3D pathfinder;
    private NpcWander3D wanderer;
    private NpcController controller;
    private NpcScheduleState model;
    private TravelRecoveryModel recovery;
    private List<Vector3> path;
    private int pathIndex;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    /// <summary>The key id this NPC carries for its home.</summary>
    public string HomeKeyId => homeKeyId;

    /// <summary>The authoritative schedule model for this NPC, when bound.</summary>
    public NpcScheduleState Model => model;

    private void Awake() => EnsureInitialized();

    // (Re)binds the runtime state. Called from Update too, so a domain reload (script recompile
    // during Play Mode) that resets this non-serialized state cannot leave the NPC frozen forever.
    private void EnsureInitialized()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (bodyCollider == null) bodyCollider = GetComponent<CapsuleCollider>();
        if (pathfinder == null) pathfinder = GetComponent<NpcPathfinder3D>();
        if (wanderer == null) wanderer = GetComponent<NpcWander3D>();
        if (controller == null) controller = GetComponent<NpcController>();
        if (recovery == null)
            recovery = new TravelRecoveryModel(recoveryConfig.StallTimeout, recoveryConfig.MaxRepaths);

        if (model != null)
            return;

        string npcId = controller != null ? controller.NpcId : gameObject.name;
        GameSessionHost.EnsureExists();
        GameSession session = GameSessionHost.Session;
        if (session != null && !string.IsNullOrWhiteSpace(npcId))
        {
            // Register returns the existing model after a reload, so a saved phase is preserved.
            model = session.NpcSchedules.Register(
                npcId,
                NpcSchedulePhase.Away,
                Random.Range(scheduleConfig.AwaySeconds * 0.3f, scheduleConfig.AwaySeconds));
        }

        // Keep the wanderer in step with the (possibly restored) phase: only Away wanders; a
        // ToHome/Home NPC is driven by the schedule instead.
        if (wanderer != null)
            wanderer.enabled = model == null || model.Phase == NpcSchedulePhase.Away;
    }

    private void Update()
    {
        EnsureInitialized();

        // Hold still while talking (dialogue, cutscene) or otherwise not idle; timers pause too.
        if (controller != null && controller.BehaviorState != NpcBehaviorState.Idle)
        {
            body.linearVelocity = Vector3.zero;
            return;
        }

        if (model == null)
            return;

        model.Tick(Time.deltaTime);

        switch (model.Phase)
        {
            case NpcSchedulePhase.Away:
                if (model.SecondsRemaining <= 0f)
                    BeginToHome();
                break;

            case NpcSchedulePhase.ToHome:
                TickToHome();
                break;

            case NpcSchedulePhase.Home:
                if (model.SecondsRemaining <= 0f)
                    BeginLeaving();
                break;
        }
    }

    private void BeginToHome()
    {
        if (wanderer != null)
            wanderer.enabled = false;

        body.linearVelocity = Vector3.zero;

        // A missing route is not permission to teleport home; the trip is cancelled and retried.
        if (!EnsureRoute())
        {
            LogDiagnostic("no route home; cancelling trip");
            CancelTrip(recoveryConfig.RetrySeconds);
            return;
        }

        recovery.Reset(body.position.x, body.position.z);
        model.SetPhase(NpcSchedulePhase.ToHome, 0f);
    }

    private void TickToHome()
    {
        // Restored mid-trip (or route lost): rebuild the route before moving, and restart stall
        // tracking at the current position so the fresh route gets its own grace period.
        if (path == null)
        {
            if (!EnsureRoute())
            {
                LogDiagnostic("route lost; cancelling trip");
                CancelTrip(recoveryConfig.RetrySeconds);
                return;
            }

            recovery.Reset(body.position.x, body.position.z);
        }

        Vector3 position = transform.position;

        // Close enough to the door: go inside instead of insisting on the exact approach cell, so the
        // NPC cannot jam against the wall beside its door.
        if (homeEntrance != null)
        {
            Vector3 toDoor = homeEntrance.position - position;
            toDoor.y = 0f;
            if (toDoor.sqrMagnitude <= homeDoorEnterRadius * homeDoorEnterRadius)
            {
                EnterHome();
                return;
            }
        }

        // Skip over any waypoints already reached this frame.
        bool atWaypoint = false;
        while (pathIndex < path.Count)
        {
            Vector3 toWaypoint = path[pathIndex] - position;
            toWaypoint.y = 0f;
            if (toWaypoint.magnitude > WaypointReached)
                break;

            pathIndex++;
            atWaypoint = true;
        }

        if (pathIndex >= path.Count)
        {
            EnterHome();
            return;
        }

        Vector3 delta = path[pathIndex] - position;
        delta.y = 0f;
        float distanceToWaypoint = delta.magnitude;
        Vector3 direction = delta / distanceToWaypoint;

        // Slide around other commuters instead of pushing through them.
        Vector3 separation = NpcLocalAvoidance.Compute(
            position, body, bodyCollider, neighborLayers, neighborSeparation, StableSeed());
        direction = NpcLocalAvoidance.Steer(direction, separation, neighborSteerStrength);

        // Do not drive into the static world; standing still lets the recovery model repath.
        if (HitsWall(direction, distanceToWaypoint))
        {
            body.linearVelocity = Vector3.zero;
            return;
        }

        body.linearVelocity = direction * movementConfig.MoveSpeed;

        TravelRecoveryDecision decision =
            recovery.Evaluate(position.x, position.z, Time.deltaTime, atWaypoint);

        if (decision == TravelRecoveryDecision.Repath)
        {
            LogDiagnostic("stalled; recomputing route");
            if (!EnsureRoute())
            {
                LogDiagnostic("repath failed; cancelling trip");
                CancelTrip(recoveryConfig.RetrySeconds);
            }
        }
        else if (decision == TravelRecoveryDecision.Abandon)
        {
            LogDiagnostic("stalled; abandoning trip");
            CancelTrip(recoveryConfig.RetrySeconds);
        }
    }

    // Builds a route to the home entrance. Returns false when no route exists. Does not touch the
    // recovery model, so repaths keep sharing the original retry budget.
    private bool EnsureRoute()
    {
        if (homeEntrance == null)
            return false;

        path = pathfinder != null
            ? pathfinder.FindPath(transform.position, homeEntrance.position)
            : new List<Vector3> { homeEntrance.position };

        if (path == null || path.Count == 0)
        {
            path = null;
            return false;
        }

        pathIndex = 0;
        return true;
    }

    private void EnterHome()
    {
        body.linearVelocity = Vector3.zero;
        path = null;

        PortalManager manager = PortalManager.Instance;
        // Route through the town door so its key requirement is enforced for the NPC.
        bool entered = string.IsNullOrWhiteSpace(homeDoorPortalId) ||
                       (manager != null && manager.TryUsePortal(homeDoorPortalId, transform));

        if (!entered)
        {
            // The owner could not get in (for example a missing key or a blocked portal). Do not
            // leave the owner's door standing open or pretend to be home; retry after a full away leg.
            LogDiagnostic("could not enter home door; retrying later");
            CancelTrip(scheduleConfig.AwaySeconds);
            return;
        }

        // The owner is home, so the door stands open for visitors.
        SetHomeDoorLocked(false);
        model.SetPhase(NpcSchedulePhase.Home, scheduleConfig.HomeSeconds);
    }

    private void BeginLeaving()
    {
        PortalManager manager = PortalManager.Instance;
        bool left = string.IsNullOrWhiteSpace(homeInteriorPortalId) ||
                    (manager != null && manager.TryUsePortal(homeInteriorPortalId, transform));

        if (!left)
        {
            // Still inside; stay Home and try again rather than walking out through the walls.
            LogDiagnostic("could not leave home; will retry");
            model.SetPhase(NpcSchedulePhase.Home, recoveryConfig.RetrySeconds);
            return;
        }

        // The owner has left, so the door locks behind them.
        SetHomeDoorLocked(true);
        path = null;
        model.SetPhase(NpcSchedulePhase.Away, scheduleConfig.AwaySeconds);
        if (wanderer != null)
            wanderer.enabled = true;
    }

    // Sphere-casts the body ahead against the pathfinder's obstacle layers so the NPC stops at a
    // wall (instead of pressing into it) and lets the recovery model repath.
    private bool HitsWall(Vector3 direction, float distance)
    {
        if (pathfinder == null || distance <= 0f)
            return false;

        float radius = bodyCollider != null ? bodyCollider.radius : 0.3f;
        float centerY = bodyCollider != null ? bodyCollider.center.y : 0.7f;
        Vector3 origin = body.position + Vector3.up * centerY + direction * 0.05f;

        int count = Physics.SphereCastNonAlloc(
            origin, radius, direction, HitBuffer, distance,
            pathfinder.ObstacleMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = HitBuffer[i].collider;
            if (hit != null && hit != bodyCollider && !hit.isTrigger)
                return true;
        }

        return false;
    }

    // A stable per-NPC seed for local-avoidance escape directions (names are unique).
    private int StableSeed() => gameObject.name.GetHashCode();

    // Ends the current trip, keeps the door locked, and resumes wandering after the retry delay.
    private void CancelTrip(float retrySeconds)
    {
        path = null;
        body.linearVelocity = Vector3.zero;
        SetHomeDoorLocked(true);
        model.SetPhase(NpcSchedulePhase.Away, retrySeconds);
        if (wanderer != null)
            wanderer.enabled = true;
    }

    // Locks or unlocks this NPC's home door. Locked = the owner's key is required.
    private void SetHomeDoorLocked(bool locked)
    {
        PortalManager manager = PortalManager.Instance;
        if (manager == null || string.IsNullOrWhiteSpace(homeDoorPortalId))
            return;

        if (manager.TryFindPortal(homeDoorPortalId, out IPortalRoute route) &&
            route.Self is PortalTrigger3D door)
        {
            door.SetRequiredKeyId(locked ? homeKeyId : string.Empty);
        }
    }

    private void LogDiagnostic(string message)
    {
        if (!logDiagnostics)
            return;

        Debug.Log($"[NpcSchedule3D] '{name}' {message}. phase={model?.Phase} " +
                  $"pos={body.position} route={pathIndex}/{(path != null ? path.Count : 0)}", this);
    }
}
