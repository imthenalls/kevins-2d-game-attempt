using UnityEngine;

[DisallowMultipleComponent]
public class NpcController : MonoBehaviour, IEntityController
{
    [Header("Identity")]
    [SerializeField] private string npcId = "npc";
    [SerializeField] private string displayName = "NPC";
    [SerializeField] private NpcType npcType = NpcType.Generic;

    [Header("Enemy Stats")]
    [SerializeField] private int enemyMaxHp = 30;

    [Header("Inventory")]
    [SerializeField] private bool hasInventory = false;
    [SerializeField, Min(1)] private int inventoryRows    = 3;
    [SerializeField, Min(1)] private int inventoryColumns = 4;

    [Header("Interaction")]
    [SerializeField] private NpcBehaviorState behaviorState = NpcBehaviorState.Idle;
    [SerializeField, Min(0.25f)] private float interactionRange = 1.5f;
    [SerializeField] private Transform interactionPoint;

    public string NpcId => npcId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    public NpcType NpcType => npcType;
    public NpcBehaviorState BehaviorState => behaviorState;
    public float InteractionRange => interactionRange;
    public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;

    public EntityStats   Stats     { get; private set; }
    public Combatant      Combatant { get; private set; }
    public InventoryModel Inventory { get; private set; }

    /// <summary>False while behavior state is Disabled (movement lock).</summary>
    public bool MovementEnabled => behaviorState != NpcBehaviorState.Disabled;

    private NpcBehaviorState _stateBeforeMovementLock = NpcBehaviorState.Idle;

    private void Awake()
    {
        if (npcType == NpcType.Enemy)
        {
            Stats = gameObject.GetComponent<EntityStats>() ?? gameObject.AddComponent<EntityStats>();
            Stats.Configure(enemyMaxHp);

            Combatant = gameObject.GetComponent<Combatant>() ?? gameObject.AddComponent<Combatant>();
        }

        if (hasInventory)
            Inventory = new InventoryModel(inventoryRows, inventoryColumns);
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
    }
}

public enum NpcBehaviorState
{
    Idle,
    Talking,
    Disabled
}

public enum NpcType
{
    Generic,
    QuestGiver,
    Vendor,
    Trainer,
    Enemy,
}