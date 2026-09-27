using Game.Core;
using UnityEngine;

/// <summary>
/// Unity facade over the engine-free <see cref="NpcMemoryModel"/>. It resolves the scene objects
/// (NpcController id, SlidingDoor gate id, IKeyHolder key ownership) and forwards every decision to
/// the Core model owned by GameSession, so the knowledge and the remember/skip/forget rules live in
/// Game.Data and are saved with the session. Knowledge is forgotten once the holder owns the
/// required key.
///
/// Unity setup:
///   1. Add to an NPC GameObject. NpcUseDoorBehavior adds one automatically if missing.
///
/// Runtime API:
///   RememberLockedGate(gate, requiredKeyId), ShouldSkipGate(gate, keys),
///   HasLockedMemory(gate), ForgetGate(gate), Clear().
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcMemory : MonoBehaviour
{
    private NpcController controller;
    private NpcMemoryModel model;

    private void Awake() => controller = GetComponent<NpcController>();

    private string NpcId =>
        controller != null && !string.IsNullOrWhiteSpace(controller.NpcId)
            ? controller.NpcId
            : gameObject.name;

    // The session owns the authoritative model. A local fallback keeps the component usable outside
    // a session (isolated prefabs/tests) without creating a second authoritative store.
    private NpcMemoryModel Model
    {
        get
        {
            if (model != null)
                return model;

            GameSessionHost.EnsureExists();
            GameSession session = GameSessionHost.Session;
            model = session != null ? session.NpcMemories.Register(NpcId) : new NpcMemoryModel();
            return model;
        }
    }

    /// <summary>Records that the given gate is locked and needs <paramref name="requiredKeyId"/>.</summary>
    public void RememberLockedGate(SlidingDoor gate, string requiredKeyId)
    {
        if (gate != null)
            Model.RememberLockedGate(gate.GateId, requiredKeyId);
    }

    /// <summary>True when this NPC has previously found the gate locked.</summary>
    public bool HasLockedMemory(SlidingDoor gate) =>
        gate != null && Model.HasLockedMemory(gate.GateId);

    /// <summary>
    /// True when the NPC should skip the gate: it remembers it locked and still lacks the key.
    /// Forgets the memory (and returns false) once the holder has the key.
    /// </summary>
    public bool ShouldSkipGate(SlidingDoor gate, IKeyHolder keys)
    {
        if (gate == null || !Model.TryGetRequiredKey(gate.GateId, out string requiredKeyId))
            return false;

        bool holdsRequiredKey = keys != null && keys.HasKey(requiredKeyId);
        return Model.ShouldSkipGate(gate.GateId, holdsRequiredKey);
    }

    /// <summary>Forgets the locked memory for one gate.</summary>
    public void ForgetGate(SlidingDoor gate)
    {
        if (gate != null)
            Model.ForgetGate(gate.GateId);
    }

    /// <summary>Forgets every gate this NPC remembers.</summary>
    public void Clear() => Model.Clear();
}
