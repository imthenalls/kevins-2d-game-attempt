using System;
using UnityEngine;

/// <summary>
/// Selects which DialogueGraphAsset an NpcDialogue component uses based on world state flags.
/// Entries are evaluated top-to-bottom; the first entry whose condition is met wins.
/// Falls back to Default Dialogue if no entry matches.
///
/// Unity setup:
///   1. Add to an NPC GameObject that already has NpcDialogue.
///   2. Clear (or leave blank) the Dialogue Asset and Dialogue Id fields on NpcDialogue
///      — this component manages dialogue selection at runtime.
///   3. Add entries to the Entries list. Each entry needs a Flag Key and a Dialogue Asset.
///   4. Assign Default Dialogue (shown before any conditions are met).
///   5. Re-evaluation happens automatically whenever any watched flag changes.
///
/// Examples:
///   Quest.RescueMerchant.Completed set → "Thank you for saving me!"
///   Quest.RescueMerchant.Completed NOT set → Default: "Please help me..."
///
///   Boss.GoblinKing.Defeated set → Sheriff: "You got him! Here's your reward."
///   Quest.Investigation.Started set → Sheriff: "Find me proof of the bandit king."
///   Default → Sheriff: "Trouble's brewing around here, stranger."
///
/// Requires NpcDialogue on the same GameObject (enforced by RequireComponent).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcDialogue))]
public class WorldStateDialogueSelector : MonoBehaviour
{
    /// <summary>One condition-to-dialogue mapping evaluated in order.</summary>
    [Serializable]
    public class DialogueEntry
    {
        [Tooltip("WorldStateKey asset. Falls back to Raw Key if unassigned.")]
        public WorldStateKey flagKey;
        [Tooltip("Fallback raw string key if no asset is assigned.")]
        public string rawKey;
        [Tooltip("When enabled, this entry is selected while the flag is ABSENT.")]
        public bool requireFlagAbsent;
        [Tooltip("The dialogue graph to use when this condition is met.")]
        public DialogueGraphAsset dialogue;

        /// <summary>Resolved key string used for flag lookup and change detection.</summary>
        public string Key => flagKey != null ? (string)flagKey : rawKey;
    }

    [Tooltip("Evaluated top-to-bottom. First matching entry wins.")]
    [SerializeField] private DialogueEntry[] entries;

    [Tooltip("Dialogue used when no entry condition is met.")]
    [SerializeField] private DialogueGraphAsset defaultDialogue;

    private NpcDialogue _npcDialogue;

    private void Awake()     => _npcDialogue = GetComponent<NpcDialogue>();
    private void OnEnable()  => WorldStateManager.OnFlagChanged += HandleFlagChanged;
    private void OnDisable() => WorldStateManager.OnFlagChanged -= HandleFlagChanged;
    private void Start()     => Refresh();

    private void HandleFlagChanged(string changedKey)
    {
        if (entries == null) return;
        foreach (var e in entries)
            if (e.Key == changedKey) { Refresh(); return; }
    }

    private void Refresh()
    {
        if (_npcDialogue == null) return;

        DialogueGraphAsset selected = defaultDialogue;

        if (entries != null)
        {
            foreach (var entry in entries)
            {
                if (entry == null || entry.dialogue == null) continue;

                bool flagSet      = WorldStateManager.Instance != null && WorldStateManager.Instance.HasFlag(entry.Key);
                bool conditionMet = entry.requireFlagAbsent ? !flagSet : flagSet;

                if (conditionMet)
                {
                    selected = entry.dialogue;
                    break;
                }
            }
        }

        _npcDialogue.SelectDialogue(selected);
    }
}
