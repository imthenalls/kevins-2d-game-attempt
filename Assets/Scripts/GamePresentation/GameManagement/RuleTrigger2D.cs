using Game.Core;
using UnityEngine;

/// <summary>
/// One-shot or repeatable trigger that calls a single boolean setter on
/// <see cref="SceneRulesManager"/> when an activator enters or exits.
///
/// Unity setup:
///   1. Add a Collider2D to the GameObject — it will be forced to isTrigger automatically.
///   2. Set <see cref="rule"/> to the rule you want to change.
///   3. Set <see cref="value"/> to the boolean state to apply on fire.
///   4. Set <see cref="fireOn"/>:
///      - <b>Enter</b>  — fires once when the activator enters (value as set).
///      - <b>Exit</b>   — fires once when the activator exits (value as set).
///      - <b>Both</b>   — fires <c>value</c> on enter, <c>!value</c> on exit.
///        Useful for "inside zone = rule ON, outside zone = rule OFF" without a full
///        zone swap (unlike <see cref="RuleZone2D"/> which swaps the whole rule set).
///   5. Enable <see cref="oneShot"/> to fire only the first time; the trigger
///      is ignored on subsequent activations until you re-enable the component.
///
/// Examples:
///   • Chest pickup → SetInventoryLocked(false): rule=InventoryLocked, value=false, fireOn=Enter, oneShot=true.
///   • Cutscene zone  → freeze player while inside: rule=PlayerMovementEnabled, value=false, fireOn=Both.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RuleTrigger2D : MonoBehaviour
{
    [Tooltip("Tag of the collider that activates this trigger.")]
    [SerializeField] private string activatorTag = "Player";

    [Tooltip("Which boolean rule to change when this trigger fires.")]
    [SerializeField] private SceneRuleTarget rule;

    [Tooltip("The boolean value to apply when the trigger fires.\n" +
             "When FireOn=Both, the exit fires the opposite value.")]
    [SerializeField] private bool value = true;

    [Tooltip("Enter = fires on enter only.\nExit = fires on exit only.\n" +
             "Both = fires 'value' on enter, '!value' on exit.")]
    [SerializeField] private RuleTriggerFireOn fireOn = RuleTriggerFireOn.Enter;

    [Tooltip("If true, the trigger fires only once and is then ignored.\n" +
             "Re-enable the component to reset it.")]
    [SerializeField] private bool oneShot;

    [Tooltip("Optional stable id. When set, a fired one-shot is remembered in WorldFacts so it " +
             "survives scene reloads and saves.")]
    [SerializeField] private string triggerId;

    // The Enter/Exit/Both and one-shot rules live in Core; this adapter only feeds observations in.
    private readonly RuleTriggerPolicy policy = new RuleTriggerPolicy();

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        RestorePersistedState();
    }

    private void OnEnable()
    {
        policy.Reset();
        RestorePersistedState();
    }

    private void RestorePersistedState()
    {
        if (string.IsNullOrWhiteSpace(triggerId) || WorldStateManager.Instance == null)
            return;

        if (WorldStateManager.Instance.HasFlag(RuleTriggerPolicy.FiredKey(triggerId)))
            policy.SeedFired(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(activatorTag)) return;
        TryFire(entered: true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(activatorTag)) return;
        TryFire(entered: false);
    }

    private void TryFire(bool entered)
    {
        // Apply silently no-ops when there is no SceneRulesManager. Check that first so a trigger
        // that never applied its rule is not permanently marked as fired.
        if (SceneRulesManager.Instance == null)
            return;

        // Resolve without committing, so the one-shot is consumed only after the rule is applied.
        if (!policy.TryResolvePending(fireOn, value, entered, oneShot, out bool appliedValue))
            return;

        if (!Apply(appliedValue))
            return;

        // The rule was applied: a one-shot may now be consumed and persisted.
        if (oneShot)
        {
            policy.CommitFired();
            if (!string.IsNullOrWhiteSpace(triggerId))
                WorldStateManager.Instance?.SetFlag(RuleTriggerPolicy.FiredKey(triggerId));
        }
    }

    private bool Apply(bool v)
    {
        var mgr = SceneRulesManager.Instance;
        if (mgr == null) return false;

        switch (rule)
        {
            case SceneRuleTarget.InventoryLocked:       mgr.SetInventoryLocked(v);       break;
            case SceneRuleTarget.SavingEnabled:         mgr.SetSavingEnabled(v);         break;
            case SceneRuleTarget.PlayerInvincible:      mgr.SetPlayerInvincible(v);      break;
            case SceneRuleTarget.PlayerAttackEnabled:   mgr.SetPlayerAttackEnabled(v);   break;
            case SceneRuleTarget.PlayerMovementEnabled: mgr.SetPlayerMovementEnabled(v); break;
            case SceneRuleTarget.EnemiesInvincible:     mgr.SetEnemiesInvincible(v);     break;
            case SceneRuleTarget.NpcAttackEnabled:      mgr.SetNpcAttackEnabled(v);      break;
            case SceneRuleTarget.NpcMovementEnabled:    mgr.SetNpcMovementEnabled(v);    break;
            case SceneRuleTarget.NpcDialogueEnabled:    mgr.SetNpcDialogueEnabled(v);    break;
            case SceneRuleTarget.PortalsBlocked:        mgr.SetPortalsBlocked(v);        break;
            case SceneRuleTarget.DotEnabled:            mgr.SetDotEnabled(v);            break;
            case SceneRuleTarget.HotEnabled:            mgr.SetHotEnabled(v);            break;
        }

        return true;
    }
}
