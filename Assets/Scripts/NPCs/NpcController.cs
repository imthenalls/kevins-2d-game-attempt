using UnityEngine;

[DisallowMultipleComponent]
public class NpcController : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string npcId = "npc";
    [SerializeField] private string displayName = "NPC";

    [Header("Interaction")]
    [SerializeField] private NpcBehaviorState behaviorState = NpcBehaviorState.Idle;
    [SerializeField, Min(0.25f)] private float interactionRange = 1.5f;
    [SerializeField] private Transform interactionPoint;

    public string NpcId => npcId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
    public NpcBehaviorState BehaviorState => behaviorState;
    public float InteractionRange => interactionRange;
    public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : transform.position;

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