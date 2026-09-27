using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// NPC behavior that paths to the nearest closed gate and tries to open it. Pathfinding avoids
/// walls and closed gates via NpcPathfinder when present; otherwise it walks straight at the
/// gate. Whether the NPC succeeds depends on its own <see cref="IKeyHolder"/> (for example
/// NpcKeyring). On a locked result the NPC records the gate in <see cref="NpcMemory"/> (which can
/// persist across saves) and gives up until it holds the required key. On success it walks
/// through the opening.
///
/// This component is a thin facade over the engine-free <see cref="NpcUseDoorModel"/> (Game.Data):
/// the model owns the Approach/PassThrough phases and the response to each door result; this
/// component only samples the world (gate proximity/motion, route state) and executes the returned
/// <see cref="NpcDoorCommand"/> (movement, gate use, memory/quest side effects).
///
/// Unity setup:
///   1. Add to an NPC GameObject alongside NpcBehaviorManager.
///   2. Requires a Rigidbody2D (Gravity Scale = 0, freeze Z rotation).
///   3. Add an NpcKeyring (optionally with Starting Key Ids) so the NPC can unlock.
///   4. Recommended: add NpcPerception (gate search) and NpcPathfinder (wall avoidance).
///   5. Set Weight, Detection Radius, and Pass Through Distance. NpcMemory is added if missing.
///
/// Runtime API:
///   OnLockedGate fires when the NPC gives up on a locked gate, for emotes/barks.
/// </summary>
public class NpcUseDoorBehavior : NpcBehaviorBase
{
    [Header("Config (Game.Data)")]
    [SerializeField] private NpcUseDoorConfig doorConfig = new NpcUseDoorConfig();

    /// <summary>Raised with the gate when the NPC finds it locked and stops trying.</summary>
    public event Action<SlidingDoor> OnLockedGate;

    private readonly NpcUseDoorModel doorModel = new NpcUseDoorModel();
    private SlidingDoor gate;
    private Vector2 passTarget;

    private readonly RouteFollower approachRoute = new RouteFollower();
    private readonly RouteFollower passRoute = new RouteFollower();

    protected override void Awake()
    {
        base.Awake();
        EnsureMemory();
    }

    protected override void Enter()
    {
        doorModel.Enter();
        approachRoute.Clear();
        passRoute.Clear();

        gate = Perception != null
            ? Perception.FindNearestGate(Body.position, doorConfig.DetectionRadius)
            : FindNearestGateFallback();

        if (gate == null || (Memory != null && Memory.ShouldSkipGate(gate, KeyHolder)))
        {
            gate = null;
            Complete();
            return;
        }

        if (Pathfinder != null)
        {
            List<Vector2> found = Pathfinder.FindPath(Body.position, gate.transform.position);
            if (found != null && found.Count > 0)
                approachRoute.SetRoute(NpcRoute.FromXY(found));
        }
    }

    protected override void TickBehavior()
    {
        if (gate == null)
        {
            Complete();
            return;
        }

        Execute(doorModel.Tick(BuildObservation(stalled: false)));
    }

    // A stall is just another observation for the model: it retries when in range, else gives up.
    protected override void OnStalled()
    {
        if (gate == null || gate.IsMoving)
            return;

        Execute(doorModel.Tick(BuildObservation(stalled: true)));
    }

    // Samples Unity state (route progress, gate motion/range) into the Core observation struct.
    private NpcDoorObservation BuildObservation(bool stalled)
    {
        bool routeExhausted = false;
        bool arrivedAtPassTarget = false;

        if (doorModel.Phase == NpcDoorPhase.Approach)
        {
            if (approachRoute.HasRoute)
            {
                approachRoute.Advance(Body.position.x, Body.position.y, doorConfig.WaypointThreshold);
                routeExhausted = approachRoute.IsComplete;
            }
        }
        else if (passRoute.HasRoute)
        {
            passRoute.Advance(Body.position.x, Body.position.y, doorConfig.WaypointThreshold);
            routeExhausted = passRoute.IsComplete;
        }
        else
        {
            arrivedAtPassTarget = Arrived(passTarget, doorConfig.WaypointThreshold);
        }

        return new NpcDoorObservation
        {
            GateMissing = gate == null,
            GateOpen = gate != null && gate.IsOpen,
            GateMoving = gate != null && gate.IsMoving,
            InRange = gate != null && gate.CanInteract(transform.position),
            RouteExhausted = routeExhausted,
            ArrivedAtPassTarget = arrivedAtPassTarget,
            Stalled = stalled,
        };
    }

    private void Execute(NpcDoorCommand command)
    {
        switch (command)
        {
            case NpcDoorCommand.FollowRoute:
                FollowCurrentRoute();
                break;

            case NpcDoorCommand.AttemptUse:
                AttemptUse();
                break;

            case NpcDoorCommand.BeginPass:
                BeginPass();
                break;

            case NpcDoorCommand.Complete:
                StopMoving();
                Complete();
                break;

            default: // None, Wait
                StopMoving();
                break;
        }
    }

    private void FollowCurrentRoute()
    {
        RouteFollower active = doorModel.Phase == NpcDoorPhase.Approach ? approachRoute : passRoute;
        if (active.HasRoute && active.TryCurrent(out float waypointX, out float waypointY))
        {
            MoveToward(new Vector2(waypointX, waypointY), Config.MoveSpeed);
            return;
        }

        if (doorModel.Phase == NpcDoorPhase.Approach)
            MoveToward(gate.transform.position, Config.MoveSpeed);
        else
            MoveToward(passTarget, Config.MoveSpeed);
    }

    private void AttemptUse()
    {
        GateUseResult result = gate.TryUse(gameObject);

        if (result == GateUseResult.Locked)
        {
            Memory?.RememberLockedGate(gate, gate.RequiredKeyId);
            OnLockedGate?.Invoke(gate);
            QuestEventBus.Raise("DoorLocked", gate.RequiredKeyId, 0);
        }

        Execute(doorModel.ResolveUse(result));
    }

    private void BeginPass()
    {
        Vector2 direction = (Vector2)gate.transform.position - Body.position;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        passTarget = (Vector2)gate.transform.position + direction.normalized * doorConfig.PassThroughDistance;
        passRoute.Clear();
        if (Pathfinder != null)
        {
            List<Vector2> found = Pathfinder.FindPath(Body.position, passTarget);
            if (found != null && found.Count > 0)
                passRoute.SetRoute(NpcRoute.FromXY(found));
        }
    }

    private SlidingDoor FindNearestGateFallback()
    {
        SlidingDoor nearest = null;
        float nearestSqr = doorConfig.DetectionRadius * doorConfig.DetectionRadius;
        Vector2 origin = Body.position;

        foreach (SlidingDoor candidate in UnityEngine.Object.FindObjectsByType<SlidingDoor>())
        {
            bool exists = candidate != null;
            if (!NpcTargetSelection.IsGateEligible(exists, exists && candidate.IsOpen))
                continue;

            float sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            if (sqr <= nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
