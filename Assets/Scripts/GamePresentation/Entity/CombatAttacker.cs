using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Melee attack component shared by the player and NPCs.
///
/// Player: enable Use Player Input — Update reads keyboard/gamepad and calls TryAttack().
/// NPC: disable Use Player Input — an AI behavior script calls TryAttack() directly.
///
/// Tuning lives in the pure-C# Game.Core.CombatAttackerConfig (Game.Data assembly); this
/// component keeps only the Unity hit-mask (LayerMask) and reads the config. Set Target Layers to
/// the layer(s) this entity is allowed to hit. Attack Range remains the NPC AI engagement
/// distance; actual damage requires the moving equipped-weapon hitbox to overlap a CombatReceiver
/// collider while the swing is active. Player input is ignored when the Weapon equipment slot is
/// empty. Player input can queue one follow-up during the configured final fraction of a swing.
/// </summary>
[DisallowMultipleComponent]
public class CombatAttacker : MonoBehaviour
{
    [Header("Config (Game.Data)")]
    [SerializeField] private CombatAttackerConfig config = new CombatAttackerConfig();

    [Header("Hit Mask (Unity)")]
    [SerializeField] private LayerMask targetLayers = Physics2D.DefaultRaycastLayers;

    /// <summary>Fired immediately when a cooldown-ready attack begins.</summary>
    public event Action OnAttackStarted;

    /// <summary>Fired after a weapon-contact hit lands. Argument is raw damage.</summary>
    public event Action<int> OnAttackLanded;

    /// <summary>Fired when the weapon-contact hit that just landed kills its target.</summary>
    public event Action OnKillLanded;

    private readonly HashSet<CombatReceiver> _hitTargetsThisSwing =
        new HashSet<CombatReceiver>();
    private EquipmentManager _equipmentManager;
    private CombatReceiver _selfReceiver;
    private float _cooldownTimer;
    private float _attackAnimationTimer;
    private bool _hasBufferedAttack;
    private bool _weaponHitWindowOpen;
    private bool _recoilAppliedThisSwing;

    /// <summary>Configured visual duration for listeners animating this attack.</summary>
    public float AttackDuration => config.AttackDuration;

    /// <summary>Legacy delay retained for compatibility with existing visual listeners.</summary>
    public float AttackWindup => config.AttackWindup;

    /// <summary>World-space distance used by melee AI to decide when to attack.</summary>
    public float AttackRange => config.AttackRange;

    /// <summary>True while the current swing may deal weapon-contact damage.</summary>
    public bool IsWeaponHitWindowOpen => _weaponHitWindowOpen;

    private void Awake()
    {
        _equipmentManager = GetComponent<EquipmentManager>();
        _selfReceiver = GetComponent<CombatReceiver>();
    }

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (_attackAnimationTimer > 0f)
        {
            _attackAnimationTimer -= Time.deltaTime;
            if (_attackAnimationTimer <= 0f)
            {
                _attackAnimationTimer = 0f;
                _weaponHitWindowOpen = false;

                if (_hasBufferedAttack && HasRequiredPlayerWeapon())
                    BeginAttack();
                else
                    _hasBufferedAttack = false;
            }
        }

        if (config.UsePlayerInput && WasAttackPressedThisFrame())
            HandlePlayerAttackInput();
    }

    private void OnDisable()
    {
        _weaponHitWindowOpen = false;
        _hasBufferedAttack = false;
    }

    /// <summary>
    /// Attempts an attack. Player-controlled attackers require an equipped Weapon item.
    /// NPC callers are still controlled by their AI and equipment/visual setup.
    /// </summary>
    public void TryAttack()
    {
        if (!isActiveAndEnabled || _cooldownTimer > 0f || _attackAnimationTimer > 0f ||
            !HasRequiredPlayerWeapon())
            return;

        BeginAttack();
    }

    /// <summary>
    /// Attempts to damage a receiver touched by the active weapon hitbox. Each receiver can be
    /// damaged at most once per swing. Called by EquippedWeaponVisual's blade overlap check.
    /// </summary>
    public bool TryApplyWeaponHit(CombatReceiver receiver)
    {
        if (!_weaponHitWindowOpen || receiver == null || !receiver.Stats.IsAlive)
            return false;
        if (!config.CanHitSelf && receiver == _selfReceiver)
            return false;
        if ((targetLayers.value & (1 << receiver.gameObject.layer)) == 0)
            return false;
        if (!_hitTargetsThisSwing.Add(receiver))
            return false;

        int totalDamage = config.AttackDamage;
        if (TryGetComponent(out EntityStats attackerStats))
            totalDamage += attackerStats.BonusAttack;

        receiver.ReceiveHit(new DamageInfo(totalDamage, gameObject));
        OnAttackLanded?.Invoke(totalDamage);
        if (!receiver.Stats.IsAlive)
            OnKillLanded?.Invoke();

        if (!_recoilAppliedThisSwing && config.SelfRecoilDamage > 0 && _selfReceiver != null)
        {
            _recoilAppliedThisSwing = true;
            _selfReceiver.ReceiveHit(new DamageInfo(config.SelfRecoilDamage, gameObject));
        }

        return true;
    }

    // Starts one attack and resets the per-swing damage and input-buffer state.
    private void BeginAttack()
    {
        _hasBufferedAttack = false;
        _hitTargetsThisSwing.Clear();
        _recoilAppliedThisSwing = false;
        _cooldownTimer = Mathf.Max(config.AttackCooldown, config.AttackDuration);
        _attackAnimationTimer = config.AttackDuration;
        _weaponHitWindowOpen = true;
        OnAttackStarted?.Invoke();
    }

    // Starts immediately when ready, or queues one follow-up during the final buffer window.
    private void HandlePlayerAttackInput()
    {
        if (!HasRequiredPlayerWeapon())
        {
            _hasBufferedAttack = false;
            return;
        }

        if (_cooldownTimer <= 0f && _attackAnimationTimer <= 0f)
        {
            BeginAttack();
            return;
        }

        if (_attackAnimationTimer <= 0f || _hasBufferedAttack)
            return;

        float duration = Mathf.Max(0.01f, config.AttackDuration);
        float normalizedProgress = 1f - Mathf.Clamp01(_attackAnimationTimer / duration);
        float bufferStart = 1f - Mathf.Clamp01(config.AttackBufferWindow);
        if (normalizedProgress >= bufferStart)
            _hasBufferedAttack = true;
    }

    // Player attack input is armed only by an actual item in the Weapon equipment slot.
    private bool HasRequiredPlayerWeapon()
    {
        if (!config.UsePlayerInput)
            return true;

        if (_equipmentManager == null)
            _equipmentManager = GetComponent<EquipmentManager>();

        return _equipmentManager != null && _equipmentManager.Model != null &&
               _equipmentManager.Model.GetEquipped(EquipSlotType.Weapon) != null;
    }

    private bool WasAttackPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;
        if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
            return true;
        return false;
#else
        return Input.GetKeyDown((KeyCode)config.LegacyAttackKeyCode);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, config.AttackRange);
    }
}
