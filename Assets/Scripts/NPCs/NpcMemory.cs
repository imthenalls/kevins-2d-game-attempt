using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small per-NPC blackboard of things the NPC has learned. The first use is remembering that a
/// gate is locked so the NPC does not keep trying it. Knowledge is forgotten automatically once
/// the NPC holds the required key. With Persist enabled the knowledge is written to
/// <see cref="WorldStateManager"/> so it survives saves and scene reloads.
///
/// Unity setup:
///   1. Add to an NPC GameObject. NpcUseDoorBehavior adds one automatically if missing.
///   2. Keep Persist enabled to remember locked gates across saves.
///
/// Runtime API:
///   RememberLockedGate(gate, requiredKeyId), ShouldSkipGate(gate, keys),
///   HasLockedMemory(gate), ForgetGate(gate), Clear().
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcMemory : MonoBehaviour
{
    [Tooltip("Write locked-gate knowledge to WorldStateManager so it survives saves.")]
    [SerializeField] private bool persist = true;

    private readonly Dictionary<SlidingDoor, string> lockedGates = new Dictionary<SlidingDoor, string>();
    private NpcController controller;

    private void Awake() => controller = GetComponent<NpcController>();

    private string NpcId =>
        controller != null && !string.IsNullOrWhiteSpace(controller.NpcId)
            ? controller.NpcId
            : gameObject.name;

    private string FactKey(SlidingDoor gate) => $"Npc.{NpcId}.GateLocked.{gate.GateId}";

    /// <summary>Records that the given gate is locked and needs <paramref name="requiredKeyId"/>.</summary>
    public void RememberLockedGate(SlidingDoor gate, string requiredKeyId)
    {
        if (gate == null || string.IsNullOrWhiteSpace(requiredKeyId))
            return;

        lockedGates[gate] = requiredKeyId;
        if (persist && WorldStateManager.Instance != null)
            WorldStateManager.Instance.SetString(FactKey(gate), requiredKeyId);
    }

    /// <summary>True when this NPC has previously found the gate locked.</summary>
    public bool HasLockedMemory(SlidingDoor gate)
    {
        if (gate == null)
            return false;
        if (lockedGates.ContainsKey(gate))
            return true;

        return persist && WorldStateManager.Instance != null &&
               WorldStateManager.Instance.HasFact(FactKey(gate));
    }

    /// <summary>
    /// True when the NPC should skip the gate: it remembers it locked and still lacks the key.
    /// Forgets the memory (and returns false) once the holder has the key.
    /// </summary>
    public bool ShouldSkipGate(SlidingDoor gate, IKeyHolder keys)
    {
        if (gate == null)
            return false;

        string required = ResolveRequiredKey(gate);
        if (string.IsNullOrWhiteSpace(required))
            return false;

        if (keys != null && keys.HasKey(required))
        {
            ForgetGate(gate);
            return false;
        }

        return true;
    }

    public void ForgetGate(SlidingDoor gate)
    {
        if (gate == null)
            return;

        lockedGates.Remove(gate);
        if (persist && WorldStateManager.Instance != null)
            WorldStateManager.Instance.ClearFact(FactKey(gate));
    }

    public void Clear() => lockedGates.Clear();

    // Finds the remembered key id in memory first, then in persisted world state.
    private string ResolveRequiredKey(SlidingDoor gate)
    {
        if (lockedGates.TryGetValue(gate, out string required))
            return required;

        if (persist && WorldStateManager.Instance != null)
        {
            string factKey = FactKey(gate);
            if (WorldStateManager.Instance.HasFact(factKey))
            {
                required = WorldStateManager.Instance.GetString(factKey);
                if (!string.IsNullOrWhiteSpace(required))
                    lockedGates[gate] = required;
                return required;
            }
        }

        return null;
    }
}
