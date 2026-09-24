using System;
using Game.Core;
using UnityEngine;

/// <summary>
/// Tracks HP and MP for any entity, delegating all authoritative values to engine-free Game.Data
/// models. HP is always owned by an <see cref="IHealthModel"/> (a bound model such as
/// <see cref="HealthModel"/>/NpcState, or a private fallback); MP is owned by a bound <see cref="Wallet"/>
/// (<see cref="ManaAccount"/>) or a private fallback <see cref="ManaAccount"/>. Equipment bonuses live
/// in <see cref="StatBonuses"/>. This component is a thin facade over those models.
///
/// Unity setup:
///   1. Add to an entity root GameObject. PlayerController2D requires it automatically.
///   2. Configure Settings (EntityStatsConfig): Max/Starting HP and MP.
///   3. To give another entity shared economic/spell mana, add Wallet to the same GameObject.
///
/// Runtime API:
///   SpendMp, RestoreMp, SetMp, IncreaseMaxMp, and OnMpChanged remain compatible.
///   BindManaWallet connects a Wallet added later at runtime; BindHealthModel binds a saveable model.
///   Call Configure immediately after AddComponent for runtime-spawned entities.
/// </summary>
public class EntityStats : MonoBehaviour
{
    [Header("Stats Config (Game.Data)")]
    [SerializeField] private EntityStatsConfig config = new EntityStatsConfig();

    // Authoritative fallback models (Game.Core). A bound external model/wallet takes precedence.
    private HealthModel _fallbackHealth;
    private ManaAccount _manaPool;
    private IHealthModel _healthModel;
    private Wallet _manaWallet;
    private bool _awakeInitialized;
    private bool _configured;

    // Last observed HP, for death-edge detection (the models do not report the previous value).
    private int _lastHp;

    // Equipment bonuses are authoritative in Game.Core.
    private readonly StatBonuses _bonuses = new StatBonuses();

    // read-only accessors
    public int Hp => _healthModel != null ? _healthModel.Hp : config.StartingHp;
    public int Mp => _manaWallet != null ? _manaWallet.Balance : (_manaPool != null ? _manaPool.Balance : config.StartingMp);
    public int MaxHp => _healthModel != null ? _healthModel.MaxHp : config.MaxHp;
    public int MaxMp => _manaWallet != null ? _manaWallet.Capacity : (_manaPool != null ? _manaPool.Capacity : config.MaxMp);
    public Wallet ManaWallet => _manaWallet;
    public bool IsAlive => _healthModel != null && _healthModel.Hp > 0;

    /// <summary>Total attack bonus from equipped items.</summary>
    public int BonusAttack => _bonuses.Attack;

    /// <summary>Total defense bonus from equipped items.</summary>
    public int BonusDefense => _bonuses.Defense;

    /// <summary>Fired whenever HP changes. Args: (currentHp, maxHp)</summary>
    public event Action<int, int> OnHpChanged;

    /// <summary>Fired whenever MP changes. Args: (currentMp, maxMp)</summary>
    public event Action<int, int> OnMpChanged;

    /// <summary>Fired when HP reaches 0.</summary>
    public event Action OnDeath;

    private void Awake() => EnsureInitialized();

    private void OnDestroy()
    {
        BindHealthModel(null);
        UnsubscribeFromManaWallet();
    }

    /// <summary>
    /// Initializes the fallback HP/MP models and binds a Wallet if one is present. Safe to call
    /// repeatedly and from another component's Awake that may run before this one.
    /// </summary>
    public void EnsureInitialized()
    {
        if (_awakeInitialized)
            return;

        _awakeInitialized = true;

        // Fallback MP pool (engine-free); any Wallet added later takes over.
        _manaPool = new ManaAccount(0);
        _manaPool.InitializeMana(
            Mathf.Clamp(config.StartingMp, 0, config.MaxMp), Mathf.Max(0, config.MaxMp));
        _manaPool.BalanceChanged += HandlePoolBalanceChanged;
        _manaPool.CapacityChanged += HandlePoolCapacityChanged;

        // Fallback HP model (engine-free). Keep any external model bound before Awake.
        _fallbackHealth = new HealthModel(config.MaxHp, Mathf.Clamp(config.StartingHp, 0, config.MaxHp));
        if (_healthModel == null)
        {
            _fallbackHealth.HpChanged += HandleModelHpChanged;
            _healthModel = _fallbackHealth;
        }
        _lastHp = _healthModel.Hp;

        if (TryGetComponent<Wallet>(out var wallet))
            BindManaWallet(wallet);
    }

    /// <summary>
    /// Binds (or clears, with null) a pure-C# health model. While bound, Hp/MaxHp read from the
    /// model and damage/heal/set delegate to it. Passing null reverts to the private fallback model,
    /// which adopts the values observed so far.
    /// </summary>
    public void BindHealthModel(IHealthModel model)
    {
        if (model == null)
            model = _fallbackHealth;
        if (_healthModel == model)
            return;

        int hp = _healthModel != null ? _healthModel.Hp : config.StartingHp;
        int max = _healthModel != null ? _healthModel.MaxHp : config.MaxHp;

        if (_healthModel != null)
            _healthModel.HpChanged -= HandleModelHpChanged;

        _healthModel = model;

        if (_healthModel != null)
        {
            _healthModel.HpChanged += HandleModelHpChanged;

            if (_healthModel == _fallbackHealth)
            {
                // Reverting to the fallback: adopt the last observed values instead of resetting.
                _fallbackHealth.SetMaxHp(max);
                _fallbackHealth.SetHp(hp);
            }

            config.MaxHp = _healthModel.MaxHp;
            _lastHp = _healthModel.Hp;
        }

        OnHpChanged?.Invoke(Hp, MaxHp);
    }

    /// <summary>
    /// Presentation refresh from a bound model. Does not write back into the model.
    /// </summary>
    public void SetHpFromModel(int value)
    {
        if (_healthModel != null && _healthModel.Hp == value)
            return;

        bool wasAlive = _lastHp > 0;
        _lastHp = Mathf.Clamp(value, 0, MaxHp);
        OnHpChanged?.Invoke(_lastHp, MaxHp);

        if (wasAlive && _lastHp == 0)
            OnDeath?.Invoke();
    }

    private void HandleModelHpChanged(int hp, int max)
    {
        bool wasAlive = _lastHp > 0;
        _lastHp = hp;
        config.MaxHp = max;
        OnHpChanged?.Invoke(hp, config.MaxHp);

        if (wasAlive && hp == 0)
            OnDeath?.Invoke();
    }

    /// <summary>
    /// Configure stats at runtime (call immediately after AddComponent).
    /// maxMp defaults to 0 — omit it for HP-only entities like enemies.
    /// </summary>
    public void Configure(int hp, int mp = 0)
    {
        config.MaxHp = hp;
        config.StartingHp = hp;
        config.MaxMp = mp;
        config.StartingMp = mp;
        _configured = true;

        // Update whichever HP model is active (a bound model takes precedence over the fallback).
        if (_healthModel != null)
        {
            _healthModel.SetMaxHp(hp);
            _healthModel.SetHp(hp);
        }
        if (_fallbackHealth != null && _fallbackHealth != _healthModel)
        {
            _fallbackHealth.SetMaxHp(hp);
            _fallbackHealth.SetHp(hp);
        }

        if (_manaPool != null)
            _manaPool.InitializeMana(mp, mp, clearHistory: false);

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

        config.MaxMp = wallet.Capacity;
        wallet.OnBalanceChanged += HandleManaBalanceChanged;
        wallet.OnCapacityChanged += HandleManaCapacityChanged;
        OnMpChanged?.Invoke(wallet.Balance, wallet.Capacity);
    }

    // ── HP ──────────────────────────────────────────────────────────────────

    /// <summary>Reduce HP by <paramref name="amount"/>. Clamps to 0.</summary>
    public void TakeDamage(int amount)
    {
        if (amount <= 0 || _healthModel == null)
            return;

        _healthModel.ApplyDamage(amount);
    }

    /// <summary>Increase HP by <paramref name="amount"/>. Clamps to maxHp.</summary>
    public void Heal(int amount)
    {
        if (amount <= 0 || _healthModel == null)
            return;

        _healthModel.Heal(amount);
    }

    /// <summary>Set HP directly (e.g. full restore on level-up).</summary>
    public void SetHp(int value)
    {
        if (_healthModel == null)
            return;

        _healthModel.SetHp(value);
    }

    // ── MP ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true and deducts <paramref name="cost"/> if enough MP is available.
    /// Returns false and leaves MP unchanged if insufficient.
    /// </summary>
    public bool SpendMp(int cost)
    {
        if (_manaWallet != null)
            return _manaWallet.TryConsumeMana(cost, "Spell or ability mana cost", "entity_stats.spend_mp");

        return _manaPool != null &&
               _manaPool.TryConsumeMana(cost, "Spell or ability mana cost", "entity_stats.spend_mp");
    }

    /// <summary>Increase MP by <paramref name="amount"/>. Clamps to maxMp.</summary>
    public void RestoreMp(int amount)
    {
        if (_manaWallet != null)
        {
            _manaWallet.RestoreMana(amount, "Mana restored", "entity_stats.restore_mp");
            return;
        }

        _manaPool?.RestoreMana(amount, "Mana restored", "entity_stats.restore_mp");
    }

    /// <summary>Set MP directly.</summary>
    public void SetMp(int value)
    {
        if (_manaWallet != null)
        {
            _manaWallet.SetBalance(value, "Mana set through EntityStats", "entity_stats.set_mp");
            return;
        }

        _manaPool?.SetBalance(value, "Mana set through EntityStats", "entity_stats.set_mp");
    }

    // ── Stat scaling ────────────────────────────────────────────────────────

    /// <summary>
    /// Set maxHp directly (delegates to the health model). Current HP clamps to the new max.
    /// </summary>
    public void SetMaxHp(int value)
    {
        _healthModel?.SetMaxHp(value);
    }

    /// <summary>
    /// Raise maxHp (e.g. on level-up). Optionally also heals the added amount.
    /// </summary>
    public void IncreaseMaxHp(int amount, bool healDelta = true)
    {
        if (amount <= 0 || _healthModel == null)
            return;

        _healthModel.SetMaxHp(_healthModel.MaxHp + amount);
        if (healDelta)
            _healthModel.Heal(amount);
    }

    /// <summary>
    /// Raise maxMp (e.g. on level-up). Optionally also restores the added amount.
    /// </summary>
    public void IncreaseMaxMp(int amount, bool restoreDelta = true)
    {
        if (amount <= 0)
            return;

        if (_manaWallet != null)
        {
            _manaWallet.IncreaseCapacity(amount, restoreDelta);
            return;
        }

        _manaPool?.IncreaseCapacity(amount, restoreDelta);
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
        _bonuses.Add(atk, def);
    }

    /// <summary>
    /// Remove stat bonuses from an unequipped item.
    /// HP and MP maximums are reduced; current values are clamped to the new maximums.
    /// </summary>
    public void RemoveStatBonus(int hp, int mp, int atk, int def)
    {
        if (hp > 0) DecreaseMaxHp(hp);
        if (mp > 0) DecreaseMaxMp(mp);
        _bonuses.Remove(atk, def);
    }

    private void DecreaseMaxHp(int amount)
    {
        if (amount <= 0 || _healthModel == null)
            return;

        _healthModel.SetMaxHp(Mathf.Max(1, _healthModel.MaxHp - amount));
    }

    private void DecreaseMaxMp(int amount)
    {
        if (amount <= 0)
            return;

        if (_manaWallet != null)
        {
            _manaWallet.DecreaseCapacity(amount);
            return;
        }

        _manaPool?.DecreaseCapacity(amount);
    }

    private void InitializeWalletFromLegacyStats(Wallet wallet)
    {
        int initialMp = _awakeInitialized || _configured
            ? (_manaPool != null ? _manaPool.Balance : config.StartingMp)
            : Mathf.Clamp(config.StartingMp, 0, config.MaxMp);
        wallet.InitializeMana(initialMp, config.MaxMp);
    }

    private void HandleManaBalanceChanged(int balance)
    {
        config.MaxMp = _manaWallet != null ? _manaWallet.Capacity : config.MaxMp;
        OnMpChanged?.Invoke(balance, config.MaxMp);
    }

    private void HandleManaCapacityChanged(int capacity)
    {
        config.MaxMp = capacity;
        OnMpChanged?.Invoke(_manaWallet != null ? _manaWallet.Balance : 0, capacity);
    }

    private void HandlePoolBalanceChanged(int balance) => OnMpChanged?.Invoke(balance, _manaPool.Capacity);

    private void HandlePoolCapacityChanged(int capacity) => OnMpChanged?.Invoke(_manaPool.Balance, capacity);

    private void UnsubscribeFromManaWallet()
    {
        if (_manaWallet == null) return;
        _manaWallet.OnBalanceChanged -= HandleManaBalanceChanged;
        _manaWallet.OnCapacityChanged -= HandleManaCapacityChanged;
    }
}
