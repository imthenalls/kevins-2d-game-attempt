using System;
using UnityEngine;

/// <summary>
/// Tracks HP and MP for any entity (player or enemy).
/// Call Configure() after AddComponent for runtime-spawned entities.
/// Subscribe to OnHpChanged / OnMpChanged for UI or gameplay reactions.
/// </summary>
public class EntityStats : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int startingHp = 100;

    [Header("MP")]
    [SerializeField] private int maxMp = 50;
    [SerializeField] private int startingMp = 50;

    // current values
    private int _hp;
    private int _mp;

    // read-only accessors
    public int Hp => _hp;
    public int Mp => _mp;
    public int MaxHp => maxHp;
    public int MaxMp => maxMp;
    public bool IsAlive => _hp > 0;

    /// <summary>Fired whenever HP changes. Args: (currentHp, maxHp)</summary>
    public event Action<int, int> OnHpChanged;

    /// <summary>Fired whenever MP changes. Args: (currentMp, maxMp)</summary>
    public event Action<int, int> OnMpChanged;

    /// <summary>Fired when HP reaches 0.</summary>
    public event Action OnDeath;

    private bool _configured;

    private void Awake()
    {
        if (!_configured)
        {
            _hp = Mathf.Clamp(startingHp, 0, maxHp);
            _mp = Mathf.Clamp(startingMp, 0, maxMp);
        }
    }

    /// <summary>
    /// Configure stats at runtime (call immediately after AddComponent).
    /// maxMp defaults to 0 — omit it for HP-only entities like enemies.
    /// </summary>
    public void Configure(int hp, int mp = 0)
    {
        maxHp = hp;
        startingHp = hp;
        maxMp = mp;
        startingMp = mp;
        _hp = hp;
        _mp = mp;
        _configured = true;
    }

    // ── HP ──────────────────────────────────────────────────────────────────

    /// <summary>Reduce HP by <paramref name="amount"/>. Clamps to 0.</summary>
    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        _hp = Mathf.Max(_hp - amount, 0);
        OnHpChanged?.Invoke(_hp, maxHp);

        if (_hp == 0)
            OnDeath?.Invoke();
    }

    /// <summary>Increase HP by <paramref name="amount"/>. Clamps to maxHp.</summary>
    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        _hp = Mathf.Min(_hp + amount, maxHp);
        OnHpChanged?.Invoke(_hp, maxHp);
    }

    /// <summary>Set HP directly (e.g. full restore on level-up).</summary>
    public void SetHp(int value)
    {
        _hp = Mathf.Clamp(value, 0, maxHp);
        OnHpChanged?.Invoke(_hp, maxHp);

        if (_hp == 0 && IsAlive)
            OnDeath?.Invoke();
    }

    // ── MP ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true and deducts <paramref name="cost"/> if enough MP is available.
    /// Returns false and leaves MP unchanged if insufficient.
    /// </summary>
    public bool SpendMp(int cost)
    {
        if (cost <= 0 || _mp < cost) return false;

        _mp -= cost;
        OnMpChanged?.Invoke(_mp, maxMp);
        return true;
    }

    /// <summary>Increase MP by <paramref name="amount"/>. Clamps to maxMp.</summary>
    public void RestoreMp(int amount)
    {
        if (amount <= 0) return;

        _mp = Mathf.Min(_mp + amount, maxMp);
        OnMpChanged?.Invoke(_mp, maxMp);
    }

    /// <summary>Set MP directly.</summary>
    public void SetMp(int value)
    {
        _mp = Mathf.Clamp(value, 0, maxMp);
        OnMpChanged?.Invoke(_mp, maxMp);
    }

    // ── Stat scaling ────────────────────────────────────────────────────────

    /// <summary>
    /// Raise maxHp (e.g. on level-up). Optionally also heals the added amount.
    /// </summary>
    public void IncreaseMaxHp(int amount, bool healDelta = true)
    {
        if (amount <= 0) return;
        maxHp += amount;
        if (healDelta) Heal(amount);
        else OnHpChanged?.Invoke(_hp, maxHp);
    }

    /// <summary>
    /// Raise maxMp (e.g. on level-up). Optionally also restores the added amount.
    /// </summary>
    public void IncreaseMaxMp(int amount, bool restoreDelta = true)
    {
        if (amount <= 0) return;
        maxMp += amount;
        if (restoreDelta) RestoreMp(amount);
        else OnMpChanged?.Invoke(_mp, maxMp);
    }
}
