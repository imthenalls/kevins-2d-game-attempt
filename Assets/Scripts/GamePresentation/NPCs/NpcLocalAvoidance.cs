using UnityEngine;

/// <summary>
/// Shared local-avoidance helper for 3D NPC movement. It computes a separation vector away from
/// solid neighbors so wanderers and commuters slide past each other instead of deadlocking, and
/// blends that vector into a desired heading. Sampling stays here in the presentation layer; the
/// surrounding stall/repath policy lives in the engine-free <c>Game.Core.TravelRecoveryModel</c>.
///
/// Unity setup: none — static helper used by NpcWander3D and NpcSchedule3D.
/// </summary>
public static class NpcLocalAvoidance
{
    private static readonly Collider[] Buffer = new Collider[32];

    /// <summary>
    /// Sums a distance-weighted repulsion from every solid neighbor on <paramref name="neighborLayers"/>
    /// within <paramref name="radius"/>. Exactly-overlapping neighbors are pushed along a direction
    /// derived from <paramref name="escapeSeed"/>, so two fully stacked NPCs pick different escapes
    /// instead of copying each other. Returns Vector3.zero when nothing is nearby.
    /// </summary>
    public static Vector3 Compute(
        Vector3 position,
        Rigidbody self,
        Collider selfCollider,
        LayerMask neighborLayers,
        float radius,
        int escapeSeed)
    {
        if (neighborLayers.value == 0 || radius <= 0f)
            return Vector3.zero;

        int count = Physics.OverlapSphereNonAlloc(
            position, radius, Buffer, neighborLayers, QueryTriggerInteraction.Ignore);

        Vector3 sum = Vector3.zero;
        int neighbors = 0;
        for (int i = 0; i < count; i++)
        {
            Collider hit = Buffer[i];
            if (hit == null || hit == selfCollider || hit.isTrigger || hit.attachedRigidbody == self)
                continue;

            Vector3 offset = position - hit.bounds.center;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < 0.0001f)
            {
                // Exactly stacked: escape along a per-NPC angle so the pair splits, not clones.
                float angle = (Mathf.Abs(escapeSeed) % 360) * Mathf.Deg2Rad;
                offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                distance = 0f;
            }
            else
            {
                offset /= distance;
            }

            sum += offset * (1f - Mathf.Clamp01(distance / radius));
            neighbors++;
        }

        return neighbors > 0 ? sum : Vector3.zero;
    }

    /// <summary>
    /// Blends a separation vector into a desired movement direction. Both are treated as XZ-plane
    /// vectors; the result is normalized when possible.
    /// </summary>
    public static Vector3 Steer(Vector3 direction, Vector3 separation, float strength)
    {
        if (separation.sqrMagnitude <= 0.0001f || strength <= 0f)
            return direction;

        Vector3 steered = direction + separation * strength;
        steered.y = 0f;
        return steered.sqrMagnitude > 0.0001f ? steered.normalized : direction;
    }
}
