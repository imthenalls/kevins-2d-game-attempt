using System;
using UnityEngine;

/// <summary>
/// Combat layer for any entity that can take hits.
/// Requires EntityStats for HP/MP tracking — attach that first,
/// or let RequireComponent add it automatically.
///
/// Usage:
///   combatant.ReceiveHit(new DamageInfo(25, attackerGameObject));
///   combatant.CombatEnabled = false; // make invincible / opt out
///
/// Events:
///   OnHit   — fires on every landed hit (hook in VFX, SFX, knockback)
///   OnDeath — fires once when HP reaches 0
///
/// Enemy death integration:
///   When an entity with NpcController (NpcType.Enemy) dies, this component
///   automatically raises QuestEventBus.Raise("EnemyKilled", npcId).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EntityStats))]
public class CombatReceiver : MonoBehaviour
{
    [Header("Combat")]
    [SerializeField] private bool combatEnabled = true;
    [Tooltip("When true, the entity takes no damage (immune to all hits).")]
    [SerializeField] private bool invincible = false;

    /// <summary>
    /// When false, ReceiveHit is a no-op — the entity cannot take damage.
    /// Toggle at runtime for cutscenes, opt-out, etc.
    /// </summary>
    public bool CombatEnabled
    {
        get => combatEnabled;
        set => combatEnabled = value;
    }

    /// <summary>
    /// When true, the entity takes no damage from any hit.
    /// Toggle at runtime or set in the Inspector for god-mode / invincibility frames.
    /// </summary>
    public bool Invincible
    {
        get => invincible;
        set => invincible = value;
    }

    /// <summary>Fired whenever this entity receives a hit. Args: (info, stats)</summary>
    public event Action<DamageInfo, EntityStats> OnHit;

    /// <summary>Fired once when this entity's HP reaches 0.</summary>
    public event Action<CombatReceiver> OnDeath;

    /// <summary>The EntityStats component on this entity.</summary>
    public EntityStats Stats { get; private set; }

    private NpcController _npcController;
    private bool _deathFired;

    private void Awake()
    {
        Stats            = GetComponent<EntityStats>();
        _npcController   = GetComponent<NpcController>();

        Stats.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (Stats != null)
            Stats.OnDeath -= HandleDeath;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a hit to this entity.
    /// No-op if the entity is already dead or CombatEnabled is false.
    /// </summary>
    public void ReceiveHit(DamageInfo info)
    {
        if (!combatEnabled || invincible || !Stats.IsAlive) return;

        Stats.TakeDamage(info.Amount);
        OnHit?.Invoke(info, Stats);
    }

    // ── Internal ────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        if (_deathFired) return;
        _deathFired = true;

        // Raise quest event when an enemy is killed.
        if (_npcController != null && _npcController.NpcType == NpcType.Enemy)
            QuestEventBus.Raise("EnemyKilled", _npcController.NpcId);

        OnDeath?.Invoke(this);
    }
}
