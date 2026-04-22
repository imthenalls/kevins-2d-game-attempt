using UnityEngine;

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
