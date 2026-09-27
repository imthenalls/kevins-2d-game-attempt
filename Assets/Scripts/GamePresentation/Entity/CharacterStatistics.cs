using System;
using Game.Core;
using UnityEngine;

/// <summary>
/// Tracks cumulative gameplay statistics for a character (player or NPC). Subscribes to
/// CombatAttacker events on the same GameObject — no polling required.
///
/// The authoritative totals live in the engine-free <see cref="CharacterStatisticsModel"/>, owned
/// per character by <see cref="GameSession"/> (via <see cref="CharacterStatisticsRepository"/>), so
/// they survive component destruction and scene loads and can be saved. This component only samples
/// combat events and re-raises convenience events.
///
/// Unity setup:
///   1. Add to any GameObject that already has a CombatAttacker.
///   2. Set Character Id to a stable value to persist/share stats; leave blank to use "player" for a
///      PlayerControllerBase or the GameObject name otherwise.
///   3. For stats that originate outside CombatAttacker (e.g. crits from a spell system),
///      call RecordCriticalHit() or RecordKill() directly on this component.
///
/// Extending without modifying this class:
///   Subscribe to the per-stat change events from any external system:
///     stats.OnAttacksChanged     += count => achievementSystem.Check("attacks", count);
///     stats.OnDamageDealtChanged += total => questTracker.Update("damage_quest", total);
///     stats.OnKillsChanged       += count => SaveManager.Instance.MarkDirty();
/// </summary>
[DisallowMultipleComponent]
public class CharacterStatistics : MonoBehaviour
{
    [Tooltip("Stable id for this character's persisted statistics. Blank = \"player\" for a " +
             "PlayerControllerBase, otherwise the GameObject name.")]
    [SerializeField] private string characterId;

    // ── Read-only stat properties (delegate to the session-owned model) ───────

    public int TotalAttacks => Model.TotalAttacks;
    public int TotalDamageDealt => Model.TotalDamageDealt;
    public int TotalKills => Model.TotalKills;
    public int CriticalHits => Model.CriticalHits;
    public int TotalItemsGathered => Model.TotalItemsGathered;
    public int TotalMoneyGained => Model.TotalMoneyGained;

    // ── Per-stat change events ────────────────────────────────────────────────

    public event Action<int> OnAttacksChanged;
    public event Action<int> OnDamageDealtChanged;
    public event Action<int> OnKillsChanged;
    public event Action<int> OnCriticalHitsChanged;
    public event Action<int> OnItemsGatheredChanged;
    public event Action<int> OnMoneyGainedChanged;

    // ── Internal ──────────────────────────────────────────────────────────────

    private CombatAttacker _attacker;
    private CharacterStatisticsModel _model;

    private CharacterStatisticsModel Model
    {
        get
        {
            if (_model != null)
                return _model;

            GameSessionHost.EnsureExists();
            _model = GameSessionHost.Session.Statistics.GetOrCreate(ResolveCharacterId());
            return _model;
        }
    }

    private void Awake()
    {
        _attacker = GetComponent<CombatAttacker>();

        // A player avatar may legitimately have no CombatAttacker in a non-combat world; its stats
        // still need to exist so item/money gains and saves are tracked. Only warn for other actors,
        // where a missing attacker is an authoring mistake.
        if (_attacker == null && !CompareTag("Player"))
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

    private string ResolveCharacterId()
    {
        if (!string.IsNullOrWhiteSpace(characterId))
            return characterId.Trim();
        return GetComponent<PlayerControllerBase>() != null ? "player" : gameObject.name;
    }

    // ── Public API for stats sourced outside CombatAttacker ──────────────────

    /// <summary>
    /// Record a critical hit from any system (spell caster, status effect, etc.).
    /// The damage dealt by the hit should already have been recorded via OnAttackLanded;
    /// this method only increments the crit counter.
    /// </summary>
    public void RecordCriticalHit()
    {
        Model.RecordCriticalHit();
        OnCriticalHitsChanged?.Invoke(Model.CriticalHits);
    }

    /// <summary>
    /// Record a kill sourced outside CombatAttacker (environment kill, DoT, etc.).
    /// CombatAttacker kills are counted automatically via the OnKillLanded event.
    /// </summary>
    public void RecordKill()
    {
        Model.RecordKill();
        OnKillsChanged?.Invoke(Model.TotalKills);
    }

    /// <summary>
    /// Record one or more items being picked up.
    /// Call from your item pickup / loot system.
    /// </summary>
    public void RecordItemGathered(int count = 1)
    {
        if (count <= 0)
            return;
        Model.RecordItemGathered(count);
        OnItemsGatheredChanged?.Invoke(Model.TotalItemsGathered);
    }

    /// <summary>
    /// Record currency gained from any source (loot, quest reward, selling, etc.).
    /// </summary>
    public void RecordMoneyGained(int amount)
    {
        if (amount <= 0)
            return;
        Model.RecordMoneyGained(amount);
        OnMoneyGainedChanged?.Invoke(Model.TotalMoneyGained);
    }

    // ── Save / load ───────────────────────────────────────────────────────────

    /// <summary>Returns the persisted statistics snapshot (used by SaveManager for the player).</summary>
    public CharacterStatisticsSnapshot GetSnapshot() => Model.GetSnapshot();

    /// <summary>Restores statistics from a save file (bulk restore; raises no events).</summary>
    public void Load(CharacterStatisticsSnapshot snapshot) => Model.Load(snapshot);

    // ── Private event handlers ────────────────────────────────────────────────

    private void HandleAttackLanded(int damage)
    {
        Model.RecordAttack(damage);
        OnAttacksChanged?.Invoke(Model.TotalAttacks);
        OnDamageDealtChanged?.Invoke(Model.TotalDamageDealt);
    }

    private void HandleKillLanded()
    {
        Model.RecordKill();
        OnKillsChanged?.Invoke(Model.TotalKills);
    }
}
