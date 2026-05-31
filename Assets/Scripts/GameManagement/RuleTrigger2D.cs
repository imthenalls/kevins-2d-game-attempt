using UnityEngine;

/// <summary>Which boolean rule to toggle via <see cref="RuleTrigger2D"/>.</summary>
public enum SceneRuleTarget
{
    InventoryLocked,
    SavingEnabled,
    PlayerInvincible,
    PlayerAttackEnabled,
    PlayerMovementEnabled,
    EnemiesInvincible,
    NpcAttackEnabled,
    NpcMovementEnabled,
    NpcDialogueEnabled,
    PortalsBlocked,
    DotEnabled,
    HotEnabled,
}

/// <summary>Which physics event fires the rule change.</summary>
public enum RuleTriggerFireOn
{
    /// <summary>Fire only when the activator enters the trigger.</summary>
    Enter,
    /// <summary>Fire only when the activator exits the trigger.</summary>
    Exit,
    /// <summary>Fire on enter with <c>value</c>, and on exit with <c>!value</c>.</summary>
    Both,
}

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

    private bool _fired;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnEnable()
    {
        _fired = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fireOn == RuleTriggerFireOn.Exit) return;
        if (!other.CompareTag(activatorTag)) return;
        Fire(value);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (fireOn == RuleTriggerFireOn.Enter) return;
        if (!other.CompareTag(activatorTag)) return;
        // Both mode inverts value on exit; Exit mode uses value as-is.
        Fire(fireOn == RuleTriggerFireOn.Both ? !value : value);
    }

    private void Fire(bool v)
    {
        if (oneShot && _fired) return;
        _fired = true;

        var mgr = SceneRulesManager.Instance;
        if (mgr == null) return;

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
    }
}
