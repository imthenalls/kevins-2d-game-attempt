using Game.Core;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// State-machine driver that picks and runs INpcBehavior components on the same GameObject.
/// Auto-discovers every INpcBehavior via GetComponents on Awake, then randomly selects
/// the next behavior by weighted probability when the current one completes.
/// Pauses automatically while the NPC is in Talking or Disabled state.
///
/// Unity setup:
///   1. Add to an NPC GameObject that also has NpcController.
///   2. Add one or more behavior components (NpcIdleBehavior, NpcWanderBehavior, or custom)
///      to the same GameObject.
///   3. Set the Weight on each behavior to control its relative selection frequency.
///   The manager runs continuously — no manual start/stop is needed at runtime.
/// </summary>
[DisallowMultipleComponent]
public class NpcBehaviorManager : MonoBehaviour
{
    private readonly List<INpcBehavior> _valid = new List<INpcBehavior>();
    private INpcBehavior _current;
    private NpcController _npcController;
    private NpcBehaviorScheduler _scheduler;
    private float[] _weights;

    private void Awake()
    {
        _npcController = GetComponent<NpcController>();

        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb is INpcBehavior b)
                _valid.Add(b);
        }

        _scheduler = new NpcBehaviorScheduler(Random.Range(1, int.MaxValue));
        _weights = new float[_valid.Count];

        if (_valid.Count == 0)
            Debug.LogWarning("[NpcBehaviorManager] No INpcBehavior components found on this GameObject.", this);
    }

    private void Start()
    {
        if (_valid.Count > 0)
            Activate(PickNext());
    }

    private void Update()
    {
        if (_current == null) return;

        // Pause while the NPC is in dialogue or disabled
        if (_npcController != null && _npcController.BehaviorState != NpcBehaviorState.Idle)
            return;

        _current.Tick();

        if (_current.IsComplete())
        {
            _current.OnExit();
            Activate(PickNext());
        }
    }

    private void Activate(INpcBehavior next)
    {
        _current = next;
        _current?.OnEnter();
    }

    private INpcBehavior PickNext()
    {
        if (_valid.Count == 0) return null;

        // Weighted selection lives in Game.Core (engine-free, unit-tested).
        for (int i = 0; i < _valid.Count; i++)
            _weights[i] = _valid[i].Weight;

        int index = _scheduler.PickNext(_weights);
        return index >= 0 ? _valid[index] : null;
    }
}
