using UnityEngine;

/// <summary>
/// Physics trigger that initiates a portal transition when a tagged collider enters.
/// Supports two modes selectable in the Inspector:
///
///   Manager Mode (Use Manager Config = true):
///     Delegates to PortalManager using a portalId defined in portals.json.
///     Supports cross-scene teleportation via SceneLoader (fade transition included).
///
///   Local Mode (Use Manager Config = false):
///     Wires directly to another PortalTrigger2D in the same scene.
///     No PortalManager needed. Calculates exit position by ExitSide + distance offset
///     or uses a manually assigned Destination Spawn Point Transform.
///
/// Unity setup:
///   1. Add to a GameObject with a Collider2D (Is Trigger set automatically on Reset).
///   2. Manager Mode: tick Use Manager Config; set Portal Id (must match portals.json).
///   3. Local Mode:   assign Destination Portal (the exit PortalTrigger2D);
///      optionally assign a Destination Spawn Point for precise exit placement.
///   4. Set Required Tag (default "Player") and Travel Cooldown.
///   5. Toggle Reset Velocity and set Exit Velocity to control exit momentum.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PortalTrigger2D : MonoBehaviour
{
    public enum ExitSide
    {
        Above,
        Below,
        Left,
        Right
    }

    [Header("Mode")]
    [SerializeField] private bool useManagerConfig;

    [Header("Manager Mode")]
    [SerializeField] private string portalId;

    [Header("Local Mode Destination")]
    [SerializeField] private PortalTrigger2D destinationPortal;
    [SerializeField] private Transform destinationSpawnPoint;
    [SerializeField] private bool spawnOutsideDestination = true;
    [SerializeField] private ExitSide exitSide = ExitSide.Right;
    [SerializeField, Min(0.01f)] private float exitDistance = 1.2f;
    [SerializeField] private bool autoSeparationDistance = true;
    [SerializeField, Min(0f)] private float separationPadding = 0.15f;

    [Header("Who Can Use This Portal")]
    [SerializeField] private string requiredTag = "Player";

    [Header("Transfer Behavior")]
    [SerializeField, Min(0f)] private float travelCooldown = 0.2f;
    [SerializeField] private bool resetVelocity = true;
    [SerializeField] private Vector2 exitVelocity = Vector2.zero;

    private float nextAllowedUseTime;

    public string PortalId => portalId;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
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

        Transform traveler = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;

        if (useManagerConfig)
        {
            if (string.IsNullOrWhiteSpace(portalId))
            {
                return;
            }

            PortalManager manager = PortalManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (manager.TryUsePortal(portalId, traveler))
            {
                BlockForSeconds(travelCooldown);
            }

            return;
        }

        if (destinationPortal == null)
        {
            return;
        }

        Vector3 targetPosition;
        if (destinationSpawnPoint != null)
        {
            targetPosition = destinationSpawnPoint.position;
        }
        else
        {
            targetPosition = destinationPortal.transform.position;
            if (spawnOutsideDestination)
            {
                float finalExitDistance = GetFinalExitDistance(traveler);
                targetPosition += ExitSideToVector(exitSide) * finalExitDistance;
            }
        }

        targetPosition.z = traveler.position.z;
        traveler.position = targetPosition;

        if (resetVelocity)
        {
            Rigidbody2D body = traveler.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = exitVelocity;
            }
        }

        BlockForSeconds(travelCooldown);
        destinationPortal.BlockForSeconds(travelCooldown);
    }

    public void BlockForSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        nextAllowedUseTime = Mathf.Max(nextAllowedUseTime, Time.time + seconds);
    }

    private static Vector3 ExitSideToVector(ExitSide side)
    {
        switch (side)
        {
            case ExitSide.Above:
                return Vector3.up;
            case ExitSide.Below:
                return Vector3.down;
            case ExitSide.Left:
                return Vector3.left;
            case ExitSide.Right:
            default:
                return Vector3.right;
        }
    }

    private float GetFinalExitDistance(Transform traveler)
    {
        float distance = exitDistance;

        if (!autoSeparationDistance)
        {
            return distance;
        }

        float destinationExtent = GetExtentAlongSide(destinationPortal != null ? destinationPortal.GetComponent<Collider2D>() : null);
        float travelerExtent = GetExtentAlongSide(traveler != null ? traveler.GetComponent<Collider2D>() : null);

        float minNonOverlappingDistance = destinationExtent + travelerExtent + separationPadding;
        return Mathf.Max(distance, minNonOverlappingDistance);
    }

    private float GetExtentAlongSide(Collider2D col)
    {
        if (col == null)
        {
            return 0f;
        }

        Bounds b = col.bounds;
        switch (exitSide)
        {
            case ExitSide.Above:
            case ExitSide.Below:
                return b.extents.y;
            case ExitSide.Left:
            case ExitSide.Right:
            default:
                return b.extents.x;
        }
    }
}
