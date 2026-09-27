using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Gives a 3D NPC a daily loop: wander for a while, then walk to a specific house's door and go
/// inside (via the portal system), stay a while, and come back out. The NPC "owns" a home door and
/// carries a key id; the door stays open while the owner is home.
///
/// This component is a thin facade over the engine-free <see cref="NpcScheduleState"/> state machine
/// (Game.Data, owned by GameSession). It performs the Unity operations the model requests (build a
/// route, use a portal, open/close the door, enable the wanderer) and reports the outcomes back as
/// <see cref="NpcScheduleEvent"/>s. Route following uses <see cref="RouteFollower"/>; stall handling
/// delegates to <see cref="TravelRecoveryModel"/>; neighbors are steered around with
/// <see cref="NpcLocalAvoidance"/>. Tuning lives in <see cref="NpcScheduleConfig"/>,
/// <see cref="NpcLocalAvoidanceConfig"/> and <see cref="NpcTravelRecoveryConfig"/>. Requires
/// <see cref="NpcPathfinder3D"/> to route to the door.
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
    [SerializeField] private NpcLocalAvoidanceConfig avoidanceConfig = new NpcLocalAvoidanceConfig();
    [SerializeField] private NpcTravelRecoveryConfig recoveryConfig = new NpcTravelRecoveryConfig();

    [Header("Unity References")]
    [Tooltip("Layers whose members this NPC steers around while commuting.")]
    [SerializeField] private LayerMask neighborLayers = 0;
    [Tooltip("Log schedule/route decisions. Development only.")]
    [SerializeField] private bool logDiagnostics = false;

    private Rigidbody body;
    private CapsuleCollider bodyCollider;
    private NpcPathfinder3D pathfinder;
    private NpcWander3D wanderer;
    private NpcController controller;
    private NpcScheduleState model;
    private TravelRecoveryModel recovery;
    private readonly RouteFollower route = new RouteFollower();
    private Vector3 desiredVelocity;
    private bool? homeDoorOpen;

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

        if (body != null)
        {
            // Commuters are villagers, so they are immovable: the player cannot shove them.
            bool enemy = controller != null && controller.NpcType == NpcType.Enemy;
            if (!enemy && !body.isKinematic)
                body.isKinematic = true;
            body.useGravity = false;
        }

        if (model != null)
            return;

        string npcId = controller != null ? controller.NpcId : gameObject.name;
        GameSessionHost.EnsureExists();
        GameSession session = GameSessionHost.Session;
        if (session != null && !string.IsNullOrWhiteSpace(npcId))
        {
            // Register returns the existing model after a reload or a load, so the saved phase and the
            // decided initial Away leg are preserved. The randomized start is decided in Core.
            model = session.NpcSchedules.Register(
                npcId,
                NpcSchedulePhase.Away,
                scheduleConfig.InitialAwaySeconds(gameObject.name.GetHashCode()));
        }
    }

    private void Update()
    {
        EnsureInitialized();

        // Hold still while talking (dialogue, cutscene) or otherwise not idle; timers pause too.
        if (controller != null && controller.BehaviorState != NpcBehaviorState.Idle)
        {
            HaltBody();
            return;
        }

        if (model == null)
            return;

        // The Core state machine decides; this component only executes the returned operations.
        Execute(model.Tick(Time.deltaTime, scheduleConfig));
        SyncWanderer();
        SyncDoor();

        if (model.Phase == NpcSchedulePhase.ToHome)
            TickToHome();
    }

    // Runs the Unity operation(s) the schedule requested. May be called recursively (a portal action
    // reports its outcome, which returns another command).
    private void Execute(NpcScheduleCommand command)
    {
        if (command == NpcScheduleCommand.None)
            return;

        if ((command & NpcScheduleCommand.RequestRoute) != 0)
            RequestRoute();

        if ((command & NpcScheduleCommand.EnterHomePortal) != 0)
            EnterHome();

        if ((command & NpcScheduleCommand.LeaveHomePortal) != 0)
            LeaveHome();

        if ((command & NpcScheduleCommand.OpenHomeDoor) != 0)
            SetHomeDoorOpen(true);

        if ((command & NpcScheduleCommand.CloseHomeDoor) != 0)
            SetHomeDoorOpen(false);
    }

    // Builds the route home; on failure it reports back so the model abandons the trip and retries.
    private void RequestRoute()
    {
        if (EnsureRoute())
        {
            recovery.Reset(body.position.x, body.position.z);
            return;
        }

        LogDiagnostic("no route home; cancelling trip");
        Execute(model.Handle(NpcScheduleEvent.RouteFailed, scheduleConfig));
    }

    private void TickToHome()
    {
        // Restored mid-trip (or route lost): rebuild the route before moving, and restart stall
        // tracking at the current position so the fresh route gets its own grace period.
        if (!route.HasRoute)
        {
            if (!EnsureRoute())
            {
                LogDiagnostic("route lost; cancelling trip");
                Execute(model.Handle(NpcScheduleEvent.RouteFailed, scheduleConfig));
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
            float enterRadius = scheduleConfig.HomeDoorEnterRadius;
            if (toDoor.sqrMagnitude <= enterRadius * enterRadius)
            {
                Execute(model.Handle(NpcScheduleEvent.ReachedEntrance, scheduleConfig));
                return;
            }
        }

        // Consume waypoints already reached this frame, then head for the current one. When the
        // route is exhausted the NPC has arrived at the door approach.
        bool atWaypoint = route.Advance(position.x, position.z, scheduleConfig.WaypointThreshold) > 0;

        if (!route.TryCurrent(out float waypointX, out float waypointZ))
        {
            Execute(model.Handle(NpcScheduleEvent.ReachedEntrance, scheduleConfig));
            return;
        }

        Vector3 delta = new Vector3(waypointX, position.y, waypointZ) - position;
        delta.y = 0f;
        float distanceToWaypoint = delta.magnitude;
        Vector3 direction = delta / distanceToWaypoint;

        // Slide around other commuters instead of pushing through them.
        Vector3 separation = NpcLocalAvoidance.Compute(
            position, body, bodyCollider, neighborLayers, avoidanceConfig.NeighborSeparation, StableSeed());
        direction = NpcLocalAvoidance.Steer(direction, separation, avoidanceConfig.NeighborSteerStrength);

        // Do not drive into the static world, but keep ticking recovery so a permanently blocked NPC
        // still repaths (then abandons the trip) instead of pressing into the wall forever.
        if (HitsWall(direction, distanceToWaypoint))
            HaltBody();
        else
            Drive(direction * movementConfig.MoveSpeed);

        TravelRecoveryDecision decision =
            recovery.Evaluate(position.x, position.z, Time.deltaTime, atWaypoint);

        if (decision == TravelRecoveryDecision.Repath)
        {
            LogDiagnostic("stalled; recomputing route");
            if (!EnsureRoute())
            {
                LogDiagnostic("repath failed; cancelling trip");
                Execute(model.Handle(NpcScheduleEvent.RouteFailed, scheduleConfig));
            }
        }
        else if (decision == TravelRecoveryDecision.Abandon)
        {
            LogDiagnostic("stalled; abandoning trip");
            Execute(model.Handle(NpcScheduleEvent.RouteFailed, scheduleConfig));
        }
    }

    // Builds a route to the home entrance. Returns false when no route exists. Does not touch the
    // recovery model, so repaths keep sharing the original retry budget.
    private bool EnsureRoute()
    {
        if (homeEntrance == null)
            return false;

        if (pathfinder == null)
        {
            route.SetRoute(new List<PathPoint> { new PathPoint(homeEntrance.position.x, homeEntrance.position.z) });
            return true;
        }

        List<Vector3> found = pathfinder.FindPath(transform.position, homeEntrance.position);
        if (found == null || found.Count == 0)
        {
            route.Clear();
            return false;
        }

        route.SetRoute(NpcRoute.FromXZ(found));
        return true;
    }

    // Uses the town door (so its key requirement is enforced), then reports the outcome to the model.
    private void EnterHome()
    {
        HaltBody();
        route.Clear();

        PortalManager manager = PortalManager.Instance;
        bool entered = string.IsNullOrWhiteSpace(homeDoorPortalId) ||
                       (manager != null && manager.TryUsePortal(homeDoorPortalId, transform));

        Execute(model.Handle(
            entered ? NpcScheduleEvent.PortalSucceeded : NpcScheduleEvent.PortalFailed, scheduleConfig));

        if (!entered)
            LogDiagnostic("could not enter home door; retrying later");
    }

    // Uses the interior portal to come back out, then reports the outcome to the model.
    private void LeaveHome()
    {
        PortalManager manager = PortalManager.Instance;
        bool left = string.IsNullOrWhiteSpace(homeInteriorPortalId) ||
                    (manager != null && manager.TryUsePortal(homeInteriorPortalId, transform));

        Execute(model.Handle(
            left ? NpcScheduleEvent.PortalSucceeded : NpcScheduleEvent.PortalFailed, scheduleConfig));

        if (left)
        {
            route.Clear();
            LogDiagnostic("left home");
        }
        else
        {
            LogDiagnostic("could not leave home; will retry");
        }
    }

    private void SyncWanderer()
    {
        if (wanderer == null)
            return;

        // Only Away wanders; a ToHome/Home NPC is driven by the schedule instead.
        bool shouldWander = model == null || model.Phase == NpcSchedulePhase.Away;
        if (wanderer.enabled != shouldWander)
            wanderer.enabled = shouldWander;
    }

    // The door is open exactly while the owner is home. Reconciled each frame so a restored phase and
    // a late-created PortalManager both take effect without a transition.
    private void SyncDoor()
    {
        bool open = model != null && model.IsHome;
        if (homeDoorOpen == open)
            return;

        SetHomeDoorOpen(open);
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

    // Applies movement: kinematic villagers are swept in FixedUpdate (so they push the player but are
    // never pushed); dynamic bodies use velocity as before.
    private void Drive(Vector3 velocity)
    {
        if (body == null)
            return;

        if (body.isKinematic)
            desiredVelocity = velocity;
        else
            body.linearVelocity = velocity;
    }

    private void HaltBody()
    {
        if (body == null)
            return;

        if (body.isKinematic)
            desiredVelocity = Vector3.zero;
        else
            body.linearVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (body != null && body.isKinematic)
            body.MovePosition(body.position + desiredVelocity * Time.fixedDeltaTime);
    }

    // Locks or unlocks this NPC's home door. Open = no key required (the owner is home).
    private void SetHomeDoorOpen(bool open)
    {
        homeDoorOpen = open;

        PortalManager manager = PortalManager.Instance;
        if (manager == null || string.IsNullOrWhiteSpace(homeDoorPortalId))
            return;

        if (manager.TryFindPortal(homeDoorPortalId, out IPortalRoute portal) &&
            portal.Self is PortalTrigger3D door)
        {
            door.SetRequiredKeyId(open ? string.Empty : homeKeyId);
        }
    }

    private void LogDiagnostic(string message)
    {
        if (!logDiagnostics)
            return;

        Debug.Log($"[NpcSchedule3D] '{name}' {message}. phase={model?.Phase} " +
                  $"pos={body.position} waypointsRemaining={route.Remaining}", this);
    }
}
