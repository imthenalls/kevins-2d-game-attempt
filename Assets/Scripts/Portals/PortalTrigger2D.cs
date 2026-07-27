using UnityEngine;

/// <summary>
/// A scene-authored portal. This component is the single source of truth for
/// portal identity, destination, arrival point, and trigger behavior.
///
/// Leave Destination Scene blank for travel inside the current scene. For
/// cross-scene travel, enter the destination scene name and the ID of the
/// PortalTrigger2D that should receive the traveler.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class PortalTrigger2D : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string portalId;

    [Header("Destination")]
    [Tooltip("Leave blank when the destination portal is in this scene.")]
    [SerializeField] private string destinationScene;
    [SerializeField] private string destinationPortalId;

    [Header("Arrival")]
    [Tooltip("Exact position where travelers arrive at this portal.")]
    [SerializeField] private Transform exitPoint;

    [Header("Who Can Use This Portal")]
    [SerializeField] private string requiredTag = "Player";

    [Header("Transfer Behavior")]
    [SerializeField, Min(0f)] private float travelCooldown = 0.2f;

    private float nextAllowedUseTime;

    public string PortalId => portalId;
    public string DestinationScene => destinationScene;
    public string DestinationPortalId => destinationPortalId;
    public Transform ExitPoint => exitPoint;
    public float TravelCooldown => travelCooldown;
    public Vector3 ArrivalPosition => exitPoint != null ? exitPoint.position : transform.position;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnValidate()
    {
        portalId = portalId != null ? portalId.Trim() : string.Empty;
        destinationScene = destinationScene != null ? destinationScene.Trim() : string.Empty;
        destinationPortalId = destinationPortalId != null ? destinationPortalId.Trim() : string.Empty;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            return;
        }

        if (Time.time < nextAllowedUseTime)
        {
            return;
        }

        Transform traveler = other.attachedRigidbody != null
            ? other.attachedRigidbody.transform
            : other.transform;

        PortalManager manager = PortalManager.Instance;
        if (manager != null && manager.TryUsePortal(this, traveler))
        {
            BlockForSeconds(travelCooldown);
        }
    }

    public void BlockForSeconds(float seconds)
    {
        if (seconds > 0f)
        {
            nextAllowedUseTime = Mathf.Max(nextAllowedUseTime, Time.time + seconds);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = exitPoint != null ? Color.cyan : Color.red;
        Vector3 arrival = ArrivalPosition;
        Gizmos.DrawLine(transform.position, arrival);
        Gizmos.DrawWireSphere(arrival, 0.2f);
    }
}
