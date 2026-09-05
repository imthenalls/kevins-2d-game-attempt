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

    [Header("Enemy Stats")]
    [SerializeField] private int enemyMaxHp = 30;
    [Tooltip("Radius at which this enemy detects the player. Scaled by SceneRulesManager.")]
    [SerializeField, Min(0.1f)] private float aggroRange = 3f;

    [Header("Enemy Health Bar")]
    [SerializeField] private bool showEnemyHealthBar = true;
    [SerializeField, Min(0f)] private float healthBarWorldOffset = 0.8f;
    [SerializeField] private Vector2 healthBarScreenSize = new Vector2(72f, 12f);

    [Header("Inventory")]
    [SerializeField] private bool hasInventory = false;
    [SerializeField, Min(1)] private int inventoryRows    = 3;
    [SerializeField, Min(1)] private int inventoryColumns = 4;
    [Tooltip("Used only when Has Inventory is enabled and no Wallet is already attached.")]
    [SerializeField, Min(0)] private int traderStartingMana = 50;
    [Tooltip("Used only when Has Inventory is enabled and no Wallet is already attached.")]
    [SerializeField, Min(0)] private int traderManaCapacity = 500;

    [Header("Interaction")]
    [SerializeField] private NpcBehaviorState behaviorState = NpcBehaviorState.Idle;
    [SerializeField, Min(0.25f)] private float interactionRange = 1.5f;
    [SerializeField] private Transform interactionPoint;

    public string NpcId => npcId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    public NpcType NpcType => npcType;
    public NpcBehaviorState BehaviorState => behaviorState;
    public float InteractionRange => interactionRange;

    /// <summary>Detection radius for enemy NPCs. Scaled at runtime by SceneRulesManager.</summary>
    public float AggroRange
    {
        get => aggroRange;
        set => aggroRange = value;
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
    private GUIStyle _healthTextStyle;

    private void Awake()
    {
        if (npcType == NpcType.Enemy)
        {
            Stats = gameObject.GetComponent<EntityStats>() ?? gameObject.AddComponent<EntityStats>();
            Stats.Configure(enemyMaxHp);

            CombatReceiver = gameObject.GetComponent<CombatReceiver>() ?? gameObject.AddComponent<CombatReceiver>();
        }

        if (hasInventory)
            EnsureInventory();
    }

    private void Start()
    {
        if (npcType == NpcType.Enemy)
            EnemyLootDrop.PrepareInventory(this);
    }

    private void OnValidate()
    {
        inventoryRows = Mathf.Max(1, inventoryRows);
        inventoryColumns = Mathf.Max(1, inventoryColumns);
        traderManaCapacity = Mathf.Max(0, traderManaCapacity);
        traderStartingMana = Mathf.Clamp(traderStartingMana, 0, traderManaCapacity);
        healthBarWorldOffset = Mathf.Max(0f, healthBarWorldOffset);
        healthBarScreenSize.x = Mathf.Max(24f, healthBarScreenSize.x);
        healthBarScreenSize.y = Mathf.Max(6f, healthBarScreenSize.y);
    }

    public bool CanInteract(Vector3 worldPosition)
    {
        if (behaviorState == NpcBehaviorState.Disabled)
        {
            return false;
        }

        return Vector2.Distance(InteractionPosition, worldPosition) <= interactionRange;
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
            Inventory = new InventoryModel(inventoryRows, inventoryColumns);

        if (ManaWallet == null)
        {
            bool hadWallet = gameObject.TryGetComponent(out Wallet wallet);
            ManaWallet = hadWallet ? wallet : gameObject.AddComponent<Wallet>();
            if (!hadWallet)
            {
                ManaWallet.InitializeMana(
                    Mathf.Clamp(traderStartingMana, 0, traderManaCapacity),
                    traderManaCapacity);
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
        Gizmos.DrawWireSphere(InteractionPosition, interactionRange);

        if (npcType == NpcType.Enemy)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(InteractionPosition, aggroRange);
        }
    }

    private void OnGUI()
    {
        if (!showEnemyHealthBar || npcType != NpcType.Enemy || Stats == null || !Stats.IsAlive)
            return;

        Camera worldCamera = Camera.main;
        if (worldCamera == null)
            return;

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(
            transform.position + Vector3.up * healthBarWorldOffset);
        if (screenPoint.z <= 0f)
            return;

        float width = healthBarScreenSize.x;
        float height = healthBarScreenSize.y;
        var outer = new Rect(
            screenPoint.x - width * 0.5f,
            Screen.height - screenPoint.y - height * 0.5f,
            width,
            height);
        var inner = new Rect(outer.x + 2f, outer.y + 2f, outer.width - 4f, outer.height - 4f);
        float hpRatio = Stats.MaxHp > 0 ? Mathf.Clamp01((float)Stats.Hp / Stats.MaxHp) : 0f;

        Color previousColor = GUI.color;
        int previousDepth = GUI.depth;
        GUI.depth = -100;

        GUI.color = Color.black;
        GUI.DrawTexture(outer, Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.04f, 0.04f, 1f);
        GUI.DrawTexture(inner, Texture2D.whiteTexture);

        if (hpRatio > 0f)
        {
            GUI.color = Color.Lerp(new Color(0.9f, 0.12f, 0.08f), new Color(0.2f, 0.85f, 0.2f), hpRatio);
            GUI.DrawTexture(new Rect(inner.x, inner.y, inner.width * hpRatio, inner.height), Texture2D.whiteTexture);
        }

        GUI.color = Color.white;
        GUI.Label(outer, $"{Stats.Hp} / {Stats.MaxHp}", GetHealthTextStyle());
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private GUIStyle GetHealthTextStyle()
    {
        if (_healthTextStyle != null)
            return _healthTextStyle;

        _healthTextStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 9,
            fontStyle = FontStyle.Bold
        };
        _healthTextStyle.normal.textColor = Color.white;
        return _healthTextStyle;
    }
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
