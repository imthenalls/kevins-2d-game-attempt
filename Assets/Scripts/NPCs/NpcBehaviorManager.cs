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

    private void Awake()
    {
        _npcController = GetComponent<NpcController>();

        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb is INpcBehavior b)
                _valid.Add(b);
        }

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

        float total = 0f;
        foreach (INpcBehavior b in _valid) total += b.Weight;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < _valid.Count; i++)
        {
            cumulative += _valid[i].Weight;
            if (roll < cumulative)
                return _valid[i];
        }

        return _valid[_valid.Count - 1];
    }
}
