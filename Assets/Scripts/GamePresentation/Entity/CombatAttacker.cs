using System;
using Game.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Melee attack component shared by the player and NPCs. Thin facade over the engine-free
/// <see cref="AttackModel"/>: this component only reads input, resolves the equipped weapon's
/// presence, and delivers damage; the cooldown, hit window, input buffer, once-per-swing hit
/// registry and recoil flag live in Game.Data and are unit-tested there.
///
/// Player: enable Use Player Input — Update reads keyboard/gamepad and calls TryAttack().
/// NPC: disable Use Player Input — an AI behavior script calls TryAttack() directly.
///
/// Unity setup:
///   1. Add to an entity with EntityStats.
///   2. Keep the hit LayerMask (Target Layers) here; tuning is on the nested CombatAttackerConfig.
///   3. Optionally add CombatReceiver / EquipmentManager for the player's weapon requirement.
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

    private AttackModel model;
    private EquipmentManager _equipmentManager;
    private CombatReceiver _selfReceiver;
    private PlayerControllerBase _playerController;

    /// <summary>Configured visual duration for listeners animating this attack.</summary>
    public float AttackDuration => model.AttackDuration;

    /// <summary>Legacy delay retained for compatibility with existing visual listeners.</summary>
    public float AttackWindup => config.AttackWindup;

    /// <summary>World-space distance used by melee AI to decide when to attack.</summary>
    public float AttackRange => model.AttackRange;

    /// <summary>True while the current swing may deal weapon-contact damage.</summary>
    public bool IsWeaponHitWindowOpen => model.IsWeaponHitWindowOpen;

    private void Awake()
    {
        model = new AttackModel(config);
        _equipmentManager = GetComponent<EquipmentManager>();
        _selfReceiver = GetComponent<CombatReceiver>();
        _playerController = GetComponent<PlayerControllerBase>();
    }

    private void Update()
    {
        bool hasWeapon = HasRequiredPlayerWeapon();

        if (model.Tick(Time.deltaTime, hasWeapon))
            OnAttackStarted?.Invoke();

        // Ignore player attack input while movement is locked (dialogue, inventory, cutscene).
        if (config.UsePlayerInput && WasAttackPressedThisFrame() &&
            (_playerController == null || _playerController.MovementEnabled))
        {
            if (model.HandleInput(hasWeapon))
                OnAttackStarted?.Invoke();
        }
    }

    private void OnDisable() => model.Reset();

    /// <summary>
    /// Attempts an attack. Player-controlled attackers require an equipped Weapon item.
    /// NPC callers are still controlled by their AI and equipment/visual setup.
    /// </summary>
    public void TryAttack()
    {
        if (!isActiveAndEnabled)
            return;

        if (model.TryBegin(HasRequiredPlayerWeapon()))
            OnAttackStarted?.Invoke();
    }

    /// <summary>
    /// Attempts to damage a receiver touched by the active weapon hitbox. Each receiver can be
    /// damaged at most once per swing. Called by EquippedWeaponVisual's blade overlap check.
    /// </summary>
    public bool TryApplyWeaponHit(CombatReceiver receiver)
    {
        if (receiver == null || !receiver.Stats.IsAlive)
            return false;
        if ((targetLayers.value & (1 << receiver.gameObject.layer)) == 0)
            return false;
        if (!model.TryRegisterHit(receiver, config.CanHitSelf, _selfReceiver))
            return false;

        int totalDamage = config.AttackDamage;
        if (TryGetComponent(out EntityStats attackerStats))
            totalDamage += attackerStats.BonusAttack;

        receiver.ReceiveHit(new DamageInfo(totalDamage, gameObject));
        OnAttackLanded?.Invoke(totalDamage);
        if (!receiver.Stats.IsAlive)
            OnKillLanded?.Invoke();

        if (config.SelfRecoilDamage > 0 && _selfReceiver != null && model.TryConsumeRecoil())
            _selfReceiver.ReceiveHit(new DamageInfo(config.SelfRecoilDamage, gameObject));

        return true;
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
