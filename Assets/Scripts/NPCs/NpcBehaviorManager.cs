using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach this to an NPC alongside one or more INpcBehavior components.
/// The manager auto-discovers every INpcBehavior on the same GameObject.
/// Set the Weight on each behavior component to control how often it is chosen.
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
