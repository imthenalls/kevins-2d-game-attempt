using UnityEngine;

/// <summary>
/// Gates one or more IInteractable MonoBehaviours by enabling or disabling them
/// based on a world state flag condition.
///
/// Since WorldObject.CanInteract() checks component.enabled, a disabled WorldObject
/// is invisible to PlayerInteractionController. This component acts as a lock
/// that flips those components on/off whenever the world state changes.
///
/// Unity setup:
///   1. Add to the same GameObject as a WorldObject (or other IInteractable).
///   2. Assign Flag Key.
///   3. Add the WorldObject (or other IInteractable MonoBehaviours) to the
///      Guarded Components list in the Inspector.
///   4. Enable Require Flag Absent to allow interaction while the flag is NOT set
///      (e.g. a door is passable until an event closes it).
///
/// For a "locked" fallback message, add a second WorldObject with the locked text
/// and use a WorldStateActivator (Invert enabled) to show it only when locked.
///
/// Examples:
///   Key.GardenGate.Obtained set → unlock the gate WorldObject.
///   Boss.GoblinKing.Defeated set → allow opening the treasure chest.
///   Quest.Main.001Completed NOT set (Require Flag Absent) → shrine is interactable
///     until the quest completes.
/// </summary>
[DisallowMultipleComponent]
public class WorldStateInteractable : MonoBehaviour
{
    [Header("Condition")]
    [Tooltip("WorldStateKey asset. Falls back to Raw Key if unassigned.")]
    [SerializeField] private WorldStateKey flagKey;
    [Tooltip("Fallback raw string key.")]
    [SerializeField] private string rawKey;
    [Tooltip("When enabled, interaction is allowed while the flag is ABSENT (inverts the condition).")]
    [SerializeField] private bool requireFlagAbsent;

    [Header("Guarded Components")]
    [Tooltip("MonoBehaviour components (IInteractable) to enable/disable based on the condition. "
           + "Typically the WorldObject on this same GameObject.")]
    [SerializeField] private MonoBehaviour[] guardedComponents;

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

        bool flagSet            = WorldStateManager.Instance != null && WorldStateManager.Instance.HasFlag(Key);
        bool interactionAllowed = requireFlagAbsent ? !flagSet : flagSet;

        if (guardedComponents == null) return;
        foreach (var comp in guardedComponents)
        {
            if (comp != null)
                comp.enabled = interactionAllowed;
        }
    }
}
