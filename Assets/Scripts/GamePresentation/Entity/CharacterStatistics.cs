using System;
using UnityEngine;

/// <summary>
/// Tracks cumulative gameplay statistics for a character (player or NPC).
/// Subscribes to CombatAttacker events on the same GameObject — no polling required.
///
/// Unity setup:
///   1. Add to any GameObject that already has a CombatAttacker.
///   2. No Inspector fields to configure — wiring is automatic.
///   3. For stats that originate outside CombatAttacker (e.g. crits from a spell system),
///      call RecordCriticalHit() or RecordKill() directly on this component.
///
/// Extending without modifying this class:
///   Subscribe to the per-stat change events from any external system:
///     stats.OnAttacksChanged     += count => achievementSystem.Check("attacks", count);
///     stats.OnDamageDealtChanged += total => questTracker.Update("damage_quest", total);
///     stats.OnKillsChanged       += count => SaveManager.Instance.MarkDirty();
///   Adding a brand-new statistic (e.g. TotalDodges) only requires adding a property +
///   event here and a call site wherever the dodge logic lives — no other files change.
/// </summary>
[DisallowMultipleComponent]
public class CharacterStatistics : MonoBehaviour
{
    // ── Read-only stat properties ─────────────────────────────────────────────

    /// <summary>Number of successful attacks landed (one per TryAttack swing that connects).</summary>
    public int TotalAttacks { get; private set; }

    /// <summary>Raw damage dealt to targets (sum of attackDamage per swing; recoil excluded).</summary>
    public int TotalDamageDealt { get; private set; }

    /// <summary>Number of killing blows dealt by this character.</summary>
    public int TotalKills { get; private set; }

    /// <summary>Number of critical hits landed. Incremented by RecordCriticalHit().</summary>
    public int CriticalHits { get; private set; }

    /// <summary>Total number of items picked up. Incremented by RecordItemGathered().</summary>
    public int TotalItemsGathered { get; private set; }

    /// <summary>Total currency earned. Incremented by RecordMoneyGained().</summary>
    public int TotalMoneyGained { get; private set; }

    // ── Per-stat change events ────────────────────────────────────────────────
    // External systems (achievements, quests, analytics, UI, save/load) subscribe
    // to exactly the events they need — zero coupling to this class's internals.

    /// <summary>Fired whenever TotalAttacks increments. Argument is the new total.</summary>
    public event Action<int> OnAttacksChanged;

    /// <summary>Fired whenever TotalDamageDealt increases. Argument is the new total.</summary>
    public event Action<int> OnDamageDealtChanged;

    /// <summary>Fired whenever TotalKills increments. Argument is the new total.</summary>
    public event Action<int> OnKillsChanged;

    /// <summary>Fired whenever CriticalHits increments. Argument is the new total.</summary>
    public event Action<int> OnCriticalHitsChanged;

    /// <summary>Fired whenever TotalItemsGathered increments. Argument is the new total.</summary>
    public event Action<int> OnItemsGatheredChanged;

    /// <summary>Fired whenever TotalMoneyGained increases. Argument is the new total.</summary>
    public event Action<int> OnMoneyGainedChanged;

    // ── Internal ──────────────────────────────────────────────────────────────

    private CombatAttacker _attacker;

    private void Awake()
    {
        _attacker = GetComponent<CombatAttacker>();

        if (_attacker == null)
            Debug.LogWarning($"[CharacterStatistics] No CombatAttacker found on '{gameObject.name}'. " +
                             "Attack/kill stats will not be tracked automatically.");
    }

    private void OnEnable()
    {
        if (_attacker == null) return;
        _attacker.OnAttackLanded += HandleAttackLanded;
        _attacker.OnKillLanded   += HandleKillLanded;
    }

    private void OnDisable()
    {
        if (_attacker == null) return;
        _attacker.OnAttackLanded -= HandleAttackLanded;
        _attacker.OnKillLanded   -= HandleKillLanded;
    }

    // ── Public API for stats sourced outside CombatAttacker ──────────────────

    /// <summary>
    /// Record a critical hit from any system (spell caster, status effect, etc.).
    /// The damage dealt by the hit should already have been recorded via OnAttackLanded;
    /// this method only increments the crit counter.
    /// </summary>
    public void RecordCriticalHit()
    {
        CriticalHits++;
        OnCriticalHitsChanged?.Invoke(CriticalHits);
    }

    /// <summary>
    /// Record a kill sourced outside CombatAttacker (environment kill, DoT, etc.).
    /// CombatAttacker kills are counted automatically via the OnKillLanded event.
    /// </summary>
    public void RecordKill()
    {
        TotalKills++;
        OnKillsChanged?.Invoke(TotalKills);
    }

    /// <summary>
    /// Record one or more items being picked up.
    /// Call from your item pickup / loot system.
    /// </summary>
    public void RecordItemGathered(int count = 1)
    {
        TotalItemsGathered += count;
        OnItemsGatheredChanged?.Invoke(TotalItemsGathered);
    }

    /// <summary>
    /// Record currency gained from any source (loot, quest reward, selling, etc.).
    /// </summary>
    public void RecordMoneyGained(int amount)
    {
        TotalMoneyGained += amount;
        OnMoneyGainedChanged?.Invoke(TotalMoneyGained);
    }

    // ── Private event handlers ────────────────────────────────────────────────

    private void HandleAttackLanded(int damage)
    {
        TotalAttacks++;
        OnAttacksChanged?.Invoke(TotalAttacks);

        TotalDamageDealt += damage;
        OnDamageDealtChanged?.Invoke(TotalDamageDealt);
    }

    private void HandleKillLanded()
    {
        TotalKills++;
        OnKillsChanged?.Invoke(TotalKills);
    }
}
