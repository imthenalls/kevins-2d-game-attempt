using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Unity adapter for local avoidance. It only samples the physics world (finds nearby solid neighbours
/// with <c>Physics.OverlapSphere</c> and builds <see cref="NeighborSample"/> values); the separation and
/// steer math is <see cref="Game.Core.LocalAvoidance"/>, shared by every NPC stack and unit-tested
/// without a scene.
///
/// Unity setup: none — static helper used by NpcWander3D and NpcSchedule3D.
///
/// Runtime API: Compute(position, self, selfCollider, neighborLayers, radius, escapeSeed) returns an
/// XZ separation vector (zero when nothing is near); Steer(direction, separation, strength) blends it.
/// </summary>
public static class NpcLocalAvoidance
{
    private const int BufferSize = 32;

    private static readonly Collider[] Buffer = new Collider[BufferSize];
    private static readonly List<NeighborSample> Samples = new List<NeighborSample>(BufferSize);

    /// <summary>
    /// Samples solid neighbours on <paramref name="neighborLayers"/> within <paramref name="radius"/>
    /// and returns the Core separation vector as an XZ world vector. Zero when nothing is near.
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

        Samples.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider hit = Buffer[i];
            if (hit == null || hit == selfCollider || hit.isTrigger || hit.attachedRigidbody == self)
                continue;

            Vector3 offset = position - hit.bounds.center;
            offset.y = 0f;
            Samples.Add(new NeighborSample(offset.x, offset.z, offset.magnitude));
        }

        if (!LocalAvoidance.TrySeparation(Samples, radius, escapeSeed, out float sepX, out float sepZ))
            return Vector3.zero;

        return new Vector3(sepX, 0f, sepZ);
    }

    /// <summary>Blends a separation vector into a desired heading (both XZ) via Core.</summary>
    public static Vector3 Steer(Vector3 direction, Vector3 separation, float strength)
    {
        LocalAvoidance.Steer(
            direction.x, direction.z, separation.x, separation.z, strength, out float x, out float z);
        return new Vector3(x, 0f, z);
    }
}
