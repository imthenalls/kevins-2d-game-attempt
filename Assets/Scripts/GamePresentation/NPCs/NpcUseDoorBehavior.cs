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

    private SlidingDoor gate;
    private int phase; // 0 = approach, 1 = pass through
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
        phase = 0;
        gate = Perception != null
            ? Perception.FindNearestGate(Body.position, doorConfig.DetectionRadius)
            : FindNearestGateFallback();

        if (gate == null || (Memory != null && Memory.ShouldSkipGate(gate, KeyHolder)))
        {
            gate = null;
            Complete();
            return;
        }

        approachRoute.Clear();
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

        if (phase == 0) TickApproach();
        else TickPass();
    }

    // If the NPC stalls, either try from where it stopped (if close enough) or give up.
    protected override void OnStalled()
    {
        if (gate != null && gate.IsMoving)
            return;

        if (phase == 0 && gate != null && gate.CanInteract(transform.position))
            AttemptUse();
        else
            Complete();
    }

    private void TickApproach()
    {
        if (gate.IsOpen)
        {
            BeginPass();
            return;
        }

        if (gate.IsMoving)
        {
            StopMoving();
            return;
        }

        // Interaction happens only within one tile of the nearest gate cell.
        if (gate.CanInteract(transform.position))
        {
            AttemptUse();
            return;
        }

        if (approachRoute.HasRoute)
        {
            Vector2 position = Body.position;
            approachRoute.Advance(position.x, position.y, doorConfig.WaypointThreshold);
            if (approachRoute.TryCurrent(out float waypointX, out float waypointY))
            {
                MoveToward(new Vector2(waypointX, waypointY), Config.MoveSpeed);
                return;
            }

            // Reached the end of the path but still not in range; wait/attempt next tick.
            StopMoving();
            if (gate.CanInteract(transform.position))
                AttemptUse();
            return;
        }

        MoveToward(gate.transform.position, Config.MoveSpeed);
    }

    private void AttemptUse()
    {
        GateUseResult result = gate.TryUse(gameObject);
        switch (result)
        {
            case GateUseResult.Opened:
                BeginPass();
                break;

            case GateUseResult.Locked:
                Memory?.RememberLockedGate(gate, gate.RequiredKeyId);
                OnLockedGate?.Invoke(gate);
                QuestEventBus.Raise("DoorLocked", gate.RequiredKeyId, 0);
                StopMoving();
                Complete();
                break;

            case GateUseResult.Busy:
                StopMoving();
                break;

            default:
                Complete();
                break;
        }
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

        phase = 1;
    }

    private void TickPass()
    {
        if (passRoute.HasRoute)
        {
            Vector2 position = Body.position;
            passRoute.Advance(position.x, position.y, doorConfig.WaypointThreshold);
            if (passRoute.TryCurrent(out float waypointX, out float waypointY))
            {
                MoveToward(new Vector2(waypointX, waypointY), Config.MoveSpeed);
                return;
            }

            StopMoving();
            Complete();
            return;
        }

        if (Arrived(passTarget, doorConfig.WaypointThreshold))
        {
            StopMoving();
            Complete();
            return;
        }

        MoveToward(passTarget, Config.MoveSpeed);
    }

    private SlidingDoor FindNearestGateFallback()
    {
        SlidingDoor nearest = null;
        float nearestSqr = doorConfig.DetectionRadius * doorConfig.DetectionRadius;
        Vector2 origin = Body.position;

        foreach (SlidingDoor candidate in UnityEngine.Object.FindObjectsByType<SlidingDoor>())
        {
            if (candidate == null || candidate.IsOpen)
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
