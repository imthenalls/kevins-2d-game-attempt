using System;
using UnityEngine;

/// <summary>
/// Tracks HP and exposes the shared mana API for any entity. When a Wallet is present,
/// MP reads and writes delegate to that canonical mana account; entities without a Wallet
/// retain a local MP pool for backward-compatible enemies and other non-economic actors.
///
/// Unity setup:
///   1. Add to an entity root GameObject. PlayerController2D requires it automatically.
///   2. Configure Max HP / Starting HP.
///   3. Configure legacy Max MP / Starting MP. PlayerController2D uses these values to
///      initialize an automatically added Wallet when no Wallet is already configured.
///   4. To give another entity shared economic/spell mana, add Wallet to the same GameObject.
///      EntityStats binds it automatically during Awake.
///
/// Runtime API:
///   SpendMp, RestoreMp, SetMp, IncreaseMaxMp, and OnMpChanged remain compatible.
///   BindManaWallet connects a Wallet added later at runtime.
///   Call Configure immediately after AddComponent for runtime-spawned entities.
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
    private Wallet _manaWallet;
    private bool _awakeInitialized;

    // equipment bonuses (tracked separately from base stats)
    private int _bonusAttack;
    private int _bonusDefense;

    // read-only accessors
    public int Hp => _hp;
    public int Mp => _manaWallet != null ? _manaWallet.Balance : _mp;
    public int MaxHp => maxHp;
    public int MaxMp => _manaWallet != null ? _manaWallet.Capacity : maxMp;
    public Wallet ManaWallet => _manaWallet;
    public bool IsAlive => _hp > 0;

    /// <summary>Total attack bonus from equipped items.</summary>
    public int BonusAttack => _bonusAttack;

    /// <summary>Total defense bonus from equipped items.</summary>
    public int BonusDefense => _bonusDefense;

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

        _awakeInitialized = true;

        if (TryGetComponent<Wallet>(out var wallet))
            BindManaWallet(wallet);
    }

    private void OnDestroy()
    {
        UnsubscribeFromManaWallet();
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

        if (_manaWallet != null)
            _manaWallet.InitializeMana(mp, mp, clearHistory: false);
    }

    /// <summary>
    /// Bind this entity's MP API to a canonical Wallet. When initializeFromStats is true,
    /// the Wallet receives the current legacy MP and maximum without a transaction.
    /// </summary>
    public void BindManaWallet(Wallet wallet, bool initializeFromStats = false)
    {
        if (wallet == null) return;

        if (_manaWallet == wallet)
        {
            if (initializeFromStats)
                InitializeWalletFromLegacyStats(wallet);
            return;
        }

        UnsubscribeFromManaWallet();
        _manaWallet = wallet;

        if (initializeFromStats)
            InitializeWalletFromLegacyStats(wallet);

        _mp = wallet.Balance;
        maxMp = wallet.Capacity;
        wallet.OnBalanceChanged += HandleManaBalanceChanged;
        wallet.OnCapacityChanged += HandleManaCapacityChanged;
        OnMpChanged?.Invoke(wallet.Balance, wallet.Capacity);
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
        if (_manaWallet != null)
            return _manaWallet.TryConsumeMana(
                cost,
                "Spell or ability mana cost",
                "entity_stats.spend_mp");

        if (cost <= 0 || _mp < cost) return false;

        _mp -= cost;
        OnMpChanged?.Invoke(_mp, maxMp);
        return true;
    }

    /// <summary>Increase MP by <paramref name="amount"/>. Clamps to maxMp.</summary>
    public void RestoreMp(int amount)
    {
        if (_manaWallet != null)
        {
            _manaWallet.RestoreMana(
                amount,
                "Mana restored",
                "entity_stats.restore_mp");
            return;
        }

        if (amount <= 0) return;

        _mp = Mathf.Min(_mp + amount, maxMp);
        OnMpChanged?.Invoke(_mp, maxMp);
    }

    /// <summary>Set MP directly.</summary>
    public void SetMp(int value)
    {
        if (_manaWallet != null)
        {
            _manaWallet.SetBalance(
                value,
                "Mana set through EntityStats",
                "entity_stats.set_mp");
            return;
        }

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

        if (_manaWallet != null)
        {
            _manaWallet.IncreaseCapacity(amount, restoreDelta);
            return;
        }

        maxMp += amount;
        if (restoreDelta) RestoreMp(amount);
        else OnMpChanged?.Invoke(_mp, maxMp);
    }

    // ── Equipment bonuses ────────────────────────────────────────────────────

    /// <summary>
    /// Apply stat bonuses from an equipped item.
    /// HP and MP maximums increase by the given amounts; current values are healed by the delta.
    /// Attack and defense bonuses are accumulated and exposed via BonusAttack / BonusDefense.
    /// </summary>
    public void ApplyStatBonus(int hp, int mp, int atk, int def)
    {
        if (hp > 0) IncreaseMaxHp(hp, healDelta: true);
        if (mp > 0) IncreaseMaxMp(mp, restoreDelta: true);
        _bonusAttack  += atk;
        _bonusDefense += def;
    }

    /// <summary>
    /// Remove stat bonuses from an unequipped item.
    /// HP and MP maximums are reduced; current values are clamped to the new maximums.
    /// </summary>
    public void RemoveStatBonus(int hp, int mp, int atk, int def)
    {
        if (hp > 0) DecreaseMaxHp(hp);
        if (mp > 0) DecreaseMaxMp(mp);
        _bonusAttack  = Mathf.Max(0, _bonusAttack  - atk);
        _bonusDefense = Mathf.Max(0, _bonusDefense - def);
    }

    private void DecreaseMaxHp(int amount)
    {
        if (amount <= 0) return;
        maxHp = Mathf.Max(1, maxHp - amount);
        _hp   = Mathf.Min(_hp, maxHp);
        OnHpChanged?.Invoke(_hp, maxHp);
    }

    private void DecreaseMaxMp(int amount)
    {
        if (amount <= 0) return;

        if (_manaWallet != null)
        {
            _manaWallet.DecreaseCapacity(amount);
            return;
        }

        maxMp = Mathf.Max(0, maxMp - amount);
        _mp   = Mathf.Min(_mp, maxMp);
        OnMpChanged?.Invoke(_mp, maxMp);
    }

    private void InitializeWalletFromLegacyStats(Wallet wallet)
    {
        int initialMp = _awakeInitialized || _configured
            ? _mp
            : Mathf.Clamp(startingMp, 0, maxMp);
        wallet.InitializeMana(initialMp, maxMp);
    }

    private void HandleManaBalanceChanged(int balance)
    {
        _mp = balance;
        OnMpChanged?.Invoke(balance, _manaWallet != null ? _manaWallet.Capacity : maxMp);
    }

    private void HandleManaCapacityChanged(int capacity)
    {
        maxMp = capacity;
        if (_manaWallet != null)
            _mp = _manaWallet.Balance;
        OnMpChanged?.Invoke(_mp, capacity);
    }

    private void UnsubscribeFromManaWallet()
    {
        if (_manaWallet == null) return;
        _manaWallet.OnBalanceChanged -= HandleManaBalanceChanged;
        _manaWallet.OnCapacityChanged -= HandleManaCapacityChanged;
    }
}
