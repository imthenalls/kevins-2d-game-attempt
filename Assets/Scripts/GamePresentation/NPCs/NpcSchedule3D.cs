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
/// the movement/teleport, and issues model commands. Schedule tuning is in
/// <see cref="NpcScheduleConfig"/>. Requires <see cref="NpcPathfinder3D"/> to route to the door.
///
/// Unity setup:
///   1. Add to an NPC root that already has Rigidbody, NpcWander3D, NpcPathfinder3D, NpcController
///      and NpcStateView.
///   2. Set Home Door Portal Id (the town door, e.g. "door_4_14"), Home Interior Portal Id
///      (the room portal, e.g. "int_4_14"), and Home Entrance (the door's approach transform).
///   3. Set the Away/Home seconds on the nested Schedule config.
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

    private Rigidbody body;
    private NpcPathfinder3D pathfinder;
    private NpcWander3D wanderer;
    private NpcController controller;
    private NpcScheduleState model;
    private List<Vector3> path;
    private int pathIndex;
    private const float WaypointReached = 0.35f;

    /// <summary>The key id this NPC carries for its home.</summary>
    public string HomeKeyId => homeKeyId;

    /// <summary>The authoritative schedule model for this NPC, when bound.</summary>
    public NpcScheduleState Model => model;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        pathfinder = GetComponent<NpcPathfinder3D>();
        wanderer = GetComponent<NpcWander3D>();
        controller = GetComponent<NpcController>();

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
    }

    private void Update()
    {
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
                FollowPath();
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

        path = pathfinder != null && homeEntrance != null
            ? pathfinder.FindPath(transform.position, homeEntrance.position)
            : null;

        if (path == null || path.Count == 0)
        {
            EnterHome();
            return;
        }

        pathIndex = 0;
        model.SetPhase(NpcSchedulePhase.ToHome, 0f);
    }

    private void FollowPath()
    {
        if (path == null || pathIndex >= path.Count)
        {
            EnterHome();
            return;
        }

        Vector3 delta = path[pathIndex] - transform.position;
        delta.y = 0f;

        if (delta.magnitude <= WaypointReached)
        {
            pathIndex++;
            return;
        }

        body.linearVelocity = delta.normalized * movementConfig.MoveSpeed;
    }

    private void EnterHome()
    {
        body.linearVelocity = Vector3.zero;

        PortalManager manager = PortalManager.Instance;
        // Route through the town door so its key requirement is enforced for the NPC.
        if (manager != null && !string.IsNullOrWhiteSpace(homeDoorPortalId))
            manager.TryUsePortal(homeDoorPortalId, transform);

        // The owner is home, so the door stands open for visitors.
        SetHomeDoorLocked(false);

        model.SetPhase(NpcSchedulePhase.Home, scheduleConfig.HomeSeconds);
    }

    private void BeginLeaving()
    {
        PortalManager manager = PortalManager.Instance;
        if (manager != null && !string.IsNullOrWhiteSpace(homeInteriorPortalId))
            manager.TryUsePortal(homeInteriorPortalId, transform);

        // The owner has left, so the door locks behind them.
        SetHomeDoorLocked(true);

        model.SetPhase(NpcSchedulePhase.Away, scheduleConfig.AwaySeconds);
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
}
