using UnityEngine;

/// <summary>
/// Simple same-scene teleporter. When a tagged collider enters the trigger it is
/// immediately moved to a destination Transform in the same scene.
/// Supports an optional relative-offset preserve so the traveler exits near the same
/// relative position they entered (useful for doorways).
///
/// Unity setup:
///   1. Add to a GameObject with a Collider2D — Is Trigger is set automatically on Reset.
///   2. Assign Destination to a Transform in the same scene (the exit point).
///   3. Set Required Tag to restrict who can trigger it (default "Player").
///   4. Adjust Travel Cooldown to prevent the traveler from instantly re-entering.
///   5. Toggle Reset Velocity and set Exit Velocity if you want to control exit momentum.
///   Note: for cross-scene teleportation use PortalTrigger2D + PortalManager instead.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Portal2D : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private Transform destination;
    [SerializeField] private bool keepRelativeOffset = true;

    [Header("Who Can Use This Portal")]
    [SerializeField] private string requiredTag = "Player";

    [Header("Transfer Behavior")]
    [SerializeField, Min(0f)] private float travelCooldown = 0.2f;
    [SerializeField] private bool resetVelocity = true;
    [SerializeField] private Vector2 exitVelocity = Vector2.zero;

    private float nextAllowedUseTime;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (destination == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
        {
            return;
        }

        if (Time.time < nextAllowedUseTime)
        {
            return;
        }

        Transform target = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;

        Vector3 targetPosition = destination.position;
        if (keepRelativeOffset)
        {
            targetPosition += target.position - transform.position;
        }

        targetPosition.z = target.position.z;
        target.position = targetPosition;

        Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
        if (targetBody != null && resetVelocity)
        {
            targetBody.linearVelocity = exitVelocity;
        }

        BlockForSeconds(travelCooldown);

        Portal2D destinationPortal = destination.GetComponent<Portal2D>();
        if (destinationPortal != null)
        {
            destinationPortal.BlockForSeconds(travelCooldown);
        }
    }

    public void BlockForSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        nextAllowedUseTime = Mathf.Max(nextAllowedUseTime, Time.time + seconds);
    }
}
