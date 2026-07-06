using System;
using UnityEngine;

/// <summary>
/// Moves an NPC to a new position and/or changes its behavior state in response to
/// world state flags. Multiple reactions can be defined and all are evaluated on Start
/// and whenever their watched flag changes.
///
/// For showing/hiding entire NPCs, use WorldStateActivator (simpler).
/// For swapping NPC dialogue, use WorldStateDialogueSelector.
/// This component handles movement and behavior state only.
///
/// Unity setup:
///   1. Add to an NPC GameObject that has NpcController.
///   2. Add entries to the Reactions list.
///   3. For each reaction:
///        a. Assign Flag Key.
///        b. Optionally assign Move To — a Transform in the scene; the NPC teleports here.
///        c. Enable Change Behavior State and pick a state if you want to freeze/unfreeze.
///   4. Reactions are applied when their condition becomes true. They are NOT reversed
///      when the condition later becomes false (this is intentional — world events
///      are one-way progressions). Add a second reaction with Invert Condition to
///      handle the reverse if needed.
///
/// Examples:
///   Merchant.Rescued set → move NPC to TownSquare transform.
///   Village.Siege.Started set → NPC behavior = Disabled (freezes NPC in place).
///   Village.Siege.Ended set → NPC behavior = Idle (resumes normal wandering).
///
/// Requires NpcController on the same GameObject (enforced by RequireComponent).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcController))]
public class WorldStateNpcReactor : MonoBehaviour
{
    /// <summary>One flag-driven reaction that can move the NPC and/or change its behavior state.</summary>
    [Serializable]
    public class Reaction
    {
        [Tooltip("WorldStateKey asset. Falls back to Raw Key if unassigned.")]
        public WorldStateKey flagKey;
        [Tooltip("Fallback raw string key.")]
        public string rawKey;
        [Tooltip("When enabled, this reaction fires when the flag is ABSENT instead of present.")]
        public bool invertCondition;

        [Header("Movement")]
        [Tooltip("Teleport the NPC to this transform's position when the condition is met. Leave blank to skip.")]
        public Transform moveTo;

        [Header("Behavior")]
        [Tooltip("Override the NPC's behavior state when the condition is met.")]
        public bool changeBehaviorState;
        [Tooltip("The behavior state to apply.")]
        public NpcBehaviorState behaviorState;

        /// <summary>Resolved key string for flag lookup and change detection.</summary>
        public string Key => flagKey != null ? (string)flagKey : rawKey;
    }

    [SerializeField] private Reaction[] reactions;

    private NpcController _npc;

    private void Awake()     => _npc = GetComponent<NpcController>();
    private void OnEnable()  => WorldStateManager.OnFlagChanged += HandleFlagChanged;
    private void OnDisable() => WorldStateManager.OnFlagChanged -= HandleFlagChanged;
    private void Start()     => ApplyAll();

    private void HandleFlagChanged(string changedKey)
    {
        if (reactions == null) return;
        foreach (var r in reactions)
            if (r.Key == changedKey) TryApplyReaction(r);
    }

    /// <summary>Evaluates and applies every reaction whose condition is currently met.</summary>
    private void ApplyAll()
    {
        if (reactions == null) return;
        foreach (var r in reactions)
            TryApplyReaction(r);
    }

    private void TryApplyReaction(Reaction r)
    {
        bool flagSet      = WorldStateManager.Instance != null && WorldStateManager.Instance.HasFlag(r.Key);
        bool conditionMet = r.invertCondition ? !flagSet : flagSet;
        if (!conditionMet) return;

        if (r.moveTo != null)
            transform.position = r.moveTo.position;

        if (r.changeBehaviorState && _npc != null)
            _npc.SetBehaviorState(r.behaviorState);
    }
}
