using Game.Core;
using UnityEngine;

/// <summary>
/// Core identity and state component for every NPC in the game.
/// Tracks the NPC's unique id, display name, type, interaction range, and behavior state.
/// For Enemy NPCs it automatically adds EntityStats and CombatReceiver components.
/// For loot/vendor NPCs it creates an InventoryModel and finds/adds a Wallet when Has Inventory
/// is enabled. Implements IEntityController and ITradeParticipant so the same gameplay and
/// atomic trade paths work for players and NPCs.
///
/// Unity setup:
///   1. Add to an NPC GameObject.
///   2. Set NPC Id (unique string used by quests, e.g. "sheriff_tom").
///   3. Set Display Name shown in dialogue.
///   4. Set NPC Type:
///        Generic     — no combat, no stats (e.g. villagers).
///        QuestGiver  — no combat; triggers quest dialogue.
///        Vendor      — enable Has Inventory for a shop inventory.
///        Trainer     — no combat, no default inventory.
///        Enemy       — auto-adds EntityStats + CombatReceiver; set Enemy Max Hp.
///   5. Optionally assign an Interaction Point child Transform to offset the
///      interaction origin (defaults to the GameObject's own position).
///   6. For trading, enable Has Inventory and configure Trader Starting Mana / Capacity.
///      Optionally add/configure Wallet manually; otherwise one is initialized from those fields.
///   7. Add NpcDialogue, NpcBehaviorManager, NpcIdleBehavior / NpcWanderBehavior as needed.
///   The cyan/gray wire sphere in Scene view shows the current interaction range.
///
/// Runtime API:
///   NpcId/TradeParticipantId identify the NPC.
///   Inventory/TradeInventory and ManaWallet/TradeWallet expose trade-owned state.
///   EnsureInventory creates inventory/Wallet ownership for JSON-configured NPCs.
///   TradeService accepts this component as a buyer or seller.
/// </summary>
[DisallowMultipleComponent]
public class NpcController : MonoBehaviour, IEntityController, ITradeParticipant
{
    [Header("Identity")]
    [SerializeField] private string npcId = "npc";
    [SerializeField] private string displayName = "NPC";
    [SerializeField] private NpcType npcType = NpcType.Generic;

    [Header("Config (Game.Data)")]
    [SerializeField] private NpcControllerConfig config = new NpcControllerConfig();

    [Header("Inventory")]
    [SerializeField] private bool hasInventory = false;

    [Header("Interaction")]
    [SerializeField] private NpcBehaviorState behaviorState = NpcBehaviorState.Idle;
    [SerializeField] private Transform interactionPoint;

    public string NpcId => npcId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    public NpcType NpcType => npcType;
    public NpcBehaviorState BehaviorState => behaviorState;
    public float InteractionRange => config.InteractionRange;

    /// <summary>Detection radius for enemy NPCs. Scaled at runtime by SceneRulesManager.</summary>
    public float AggroRange
    {
        get => config.AggroRange;
        set => config.AggroRange = value;
    }
    public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;

    public EntityStats   Stats     { get; private set; }
    public CombatReceiver CombatReceiver { get; private set; }
    public InventoryModel Inventory { get; private set; }
    public Wallet ManaWallet { get; private set; }
    public string TradeParticipantId => npcId;
    public Wallet TradeWallet => ManaWallet;
    public InventoryModel TradeInventory => Inventory;

    /// <summary>False while behavior state is Disabled (movement lock).</summary>
    public bool MovementEnabled => behaviorState != NpcBehaviorState.Disabled;

    private NpcBehaviorState _stateBeforeMovementLock = NpcBehaviorState.Idle;
    private void Awake()
    {
        if (npcType == NpcType.Enemy)
        {
            Stats = gameObject.GetComponent<EntityStats>() ?? gameObject.AddComponent<EntityStats>();
            Stats.Configure(config.EnemyMaxHp);

            CombatReceiver = gameObject.GetComponent<CombatReceiver>() ?? gameObject.AddComponent<CombatReceiver>();
        }

        if (hasInventory)
            EnsureInventory();
    }

    private void Start()
    {
        if (npcType == NpcType.Enemy)
        {
            EnemyLootDrop.PrepareInventory(this);
            if (config.ShowEnemyHealthBar)
                EnemyHealthBarUI.Create(this);
        }
    }

    private void OnValidate()
    {
        config.InventoryRows = Mathf.Max(1, config.InventoryRows);
        config.InventoryColumns = Mathf.Max(1, config.InventoryColumns);
        config.TraderManaCapacity = Mathf.Max(0, config.TraderManaCapacity);
        config.TraderStartingMana = Mathf.Clamp(config.TraderStartingMana, 0, config.TraderManaCapacity);
        config.HealthBarWorldOffset = Mathf.Max(0f, config.HealthBarWorldOffset);
        config.HealthBarScreenWidth = Mathf.Max(24f, config.HealthBarScreenWidth);
        config.HealthBarScreenHeight = Mathf.Max(6f, config.HealthBarScreenHeight);
    }

    public bool CanInteract(Vector3 worldPosition)
    {
        if (behaviorState == NpcBehaviorState.Disabled)
        {
            return false;
        }

        return Vector2.Distance(InteractionPosition, worldPosition) <= config.InteractionRange;
    }

    public void SetBehaviorState(NpcBehaviorState newState)
    {
        behaviorState = newState;
    }

    /// <summary>
    /// Hides a defeated NPC's body, equipped weapon, and physics colliders while leaving
    /// its controller alive for save state and death-event bookkeeping.
    /// </summary>
    public void HideDefeatedBody()
    {
        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            spriteRenderer.enabled = false;

        foreach (Collider2D bodyCollider in GetComponentsInChildren<Collider2D>(true))
            bodyCollider.enabled = false;
    }

    /// <summary>
    /// Creates this NPC's inventory and Wallet if they do not already exist.
    /// Called automatically for Inspector-enabled inventories and by
    /// NpcInventoryDatabase for NPCs configured in npc_inventories.json.
    /// </summary>
    public InventoryModel EnsureInventory()
    {
        hasInventory = true;
        if (Inventory == null)
            Inventory = new InventoryModel(config.InventoryRows, config.InventoryColumns);

        if (ManaWallet == null)
        {
            bool hadWallet = gameObject.TryGetComponent(out Wallet wallet);
            ManaWallet = hadWallet ? wallet : gameObject.AddComponent<Wallet>();
            if (!hadWallet)
            {
                ManaWallet.InitializeMana(
                    Mathf.Clamp(config.TraderStartingMana, 0, config.TraderManaCapacity),
                    config.TraderManaCapacity);
            }
        }

        return Inventory;
    }

    /// <summary>
    /// Lock or unlock NPC movement by toggling behavior state.
    /// Saves and restores the previous state so callers don't need to track it.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        if (!enabled)
        {
            _stateBeforeMovementLock = behaviorState;
            behaviorState = NpcBehaviorState.Disabled;
        }
        else
        {
            behaviorState = _stateBeforeMovementLock;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = behaviorState == NpcBehaviorState.Disabled ? Color.gray : Color.cyan;
        Gizmos.DrawWireSphere(InteractionPosition, config.InteractionRange);

        if (npcType == NpcType.Enemy)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(InteractionPosition, config.AggroRange);
        }
    }


    /// <summary>Health bar configuration read by EnemyHealthBarUI; values live in Game.Data config.</summary>
    public bool ShowEnemyHealthBar => config.ShowEnemyHealthBar;
    public float HealthBarWorldOffset => config.HealthBarWorldOffset;
    public float HealthBarScreenWidth => config.HealthBarScreenWidth;
    public float HealthBarScreenHeight => config.HealthBarScreenHeight;
}
public enum NpcBehaviorState
{
    Idle,
    Talking,
    Disabled,
    Combat
}

public enum NpcType
{
    Generic,
    QuestGiver,
    Vendor,
    Trainer,
    Enemy,
}