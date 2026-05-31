using UnityEngine;

/// <summary>
/// Trigger zone that swaps the active SceneRules when an activator enters or exits.
/// Pushes <see cref="enterRules"/> onto the SceneRulesManager override stack when
/// the activator enters, and pops it (or pushes <see cref="exitRules"/>) when it exits.
///
/// Unity setup:
///   1. Add a Collider2D to the GameObject — it will be forced to isTrigger automatically.
///   2. Set <see cref="enterRules"/> to the SceneRules asset to apply inside the zone.
///   3. Optionally set <see cref="exitRules"/> if you want a distinct rule set on exit
///      (e.g. a "safe zone" transition). Leave it empty to simply pop the enter rules.
///   4. The activator tag defaults to "Player"; change it if another tag should trigger it.
///
/// Stacking:
///   Multiple overlapping RuleZone2D zones work correctly. Each zone independently
///   pushes and pops its own entry on the override stack, so the last zone entered
///   always wins while still restoring correctly as each zone is exited.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RuleZone2D : MonoBehaviour
{
    [Tooltip("Tag of the collider that activates this zone.")]
    [SerializeField] private string activatorTag = "Player";

    [Tooltip("Rules applied when the activator enters the zone.")]
    [SerializeField] private SceneRules enterRules;

    [Tooltip("Rules applied when the activator exits the zone.\n" +
             "Leave empty to pop the enter rules and revert to the previous layer.")]
    [SerializeField] private SceneRules exitRules;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(activatorTag)) return;
        if (enterRules == null) return;

        SceneRulesManager.Instance?.PushOverride(enterRules);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(activatorTag)) return;

        if (exitRules != null)
            SceneRulesManager.Instance?.PushOverride(exitRules);
        else
            SceneRulesManager.Instance?.PopOverride(enterRules);
    }
}
