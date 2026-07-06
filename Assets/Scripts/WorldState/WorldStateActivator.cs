using UnityEngine;

/// <summary>
/// Enables or disables a target GameObject based on a world state flag.
/// Evaluates once on Start (for initial scene state after load) and again
/// whenever the flag changes at runtime via WorldStateManager.OnFlagChanged.
///
/// Unity setup:
///   1. Add to any scene GameObject.
///   2. Assign Flag Key — drag a WorldStateKey asset, or type a fallback Raw Key string.
///   3. Leave Target blank to control this GameObject itself, or assign a different one.
///   4. Enable Invert to activate the object when the flag is ABSENT instead of present.
///
/// Examples:
///   Bridge.Fixed set          → enable the bridge GameObject.
///   Merchant.Rescued set      → enable the merchant NPC in the town scene.
///   Boss.GoblinKing.Defeated
///     + Invert enabled        → disable the pre-fight boss trigger.
///
/// Note: On initial scene load WorldStateManager.LoadSnapshot suppresses events,
///   so Start() is responsible for applying the correct initial state.
/// </summary>
[DisallowMultipleComponent]
public class WorldStateActivator : MonoBehaviour
{
    [Header("Condition")]
    [Tooltip("WorldStateKey asset defining the flag to watch. Falls back to Raw Key if unassigned.")]
    [SerializeField] private WorldStateKey flagKey;
    [Tooltip("Fallback raw string key used when Flag Key asset is not assigned.")]
    [SerializeField] private string rawKey;
    [Tooltip("When enabled, the target is ACTIVE while the flag is ABSENT (inverts the logic).")]
    [SerializeField] private bool invert;

    [Header("Target")]
    [Tooltip("The GameObject to show or hide. Leave blank to control this GameObject.")]
    [SerializeField] private GameObject target;

    private string Key => flagKey != null ? (string)flagKey : rawKey;

    private void OnEnable()  => WorldStateManager.OnFlagChanged += HandleFlagChanged;
    private void OnDisable() => WorldStateManager.OnFlagChanged -= HandleFlagChanged;
    private void Start()     => Refresh();

    private void HandleFlagChanged(string changedKey)
    {
        if (changedKey == Key) Refresh();
    }

    private void Refresh()
    {
        if (string.IsNullOrEmpty(Key)) return;

        bool flagSet       = WorldStateManager.Instance != null && WorldStateManager.Instance.HasFlag(Key);
        bool shouldBeActive = invert ? !flagSet : flagSet;

        GameObject go = target != null ? target : gameObject;
        if (go.activeSelf != shouldBeActive)
            go.SetActive(shouldBeActive);
    }
}
