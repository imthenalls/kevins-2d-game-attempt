using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches nearby world information for NPC behaviors in one place, so behaviors don't each run
/// their own physics queries. Scans on a fixed cadence and exposes the results as concrete lists.
///
/// Unity setup:
///   1. Add to an NPC GameObject that has NpcBehaviorManager (and a Rigidbody2D for movement).
///   2. Set Detection Layers to the physics layers worth scanning (interactables, player, NPCs).
///   3. Set Scan Radius to the largest distance any behavior needs.
///   Behaviors read Player, Gates, and Contacts, or call FindNearestGate / FindNearest.
///
/// Runtime API:
///   Refresh() (called automatically each FixedUpdate), EnsureFresh(), Player, Gates, Contacts,
///   FindNearestGate(origin, maxDistance), FindNearest&lt;T&gt;(origin, maxDistance).
/// </summary>
[DisallowMultipleComponent]
public class NpcPerception : MonoBehaviour
{
    [SerializeField] private LayerMask detectionLayers = ~0;
    [SerializeField, Min(0.5f)] private float scanRadius = 8f;

    private readonly List<Collider2D> buffer = new List<Collider2D>();
    private readonly List<Collider2D> contacts = new List<Collider2D>();
    private readonly List<SlidingDoor> gates = new List<SlidingDoor>();
    private int lastRefreshFrame = -1;

    /// <summary>Nearest player transform seen in the last scan, if any.</summary>
    public Transform Player { get; private set; }

    /// <summary>All colliders seen in the last scan. Empty until the first Refresh.</summary>
    public List<Collider2D> Contacts => contacts;

    /// <summary>Distinct SlidingDoors seen in the last scan.</summary>
    public List<SlidingDoor> Gates => gates;

    private void FixedUpdate() => Refresh();

    /// <summary>Re-scans the surroundings once per frame at most.</summary>
    public void EnsureFresh()
    {
        if (lastRefreshFrame != Time.frameCount)
            Refresh();
    }

    /// <summary>Immediately re-scans the surroundings and clears previous results.</summary>
    public void Refresh()
    {
        lastRefreshFrame = Time.frameCount;
        contacts.Clear();
        gates.Clear();
        Player = null;

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = detectionLayers, useTriggers = true };
        int count = Physics2D.OverlapCircle(transform.position, scanRadius, filter, buffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D contact = buffer[i];
            if (contact == null)
                continue;

            contacts.Add(contact);

            SlidingDoor gate = contact.GetComponentInParent<SlidingDoor>();
            if (gate != null && !gates.Contains(gate))
                gates.Add(gate);

            if (Player == null)
            {
                PlayerController2D player = contact.GetComponentInParent<PlayerController2D>();
                if (player != null)
                    Player = player.transform;
            }
        }
    }

    /// <summary>Nearest closed gate within <paramref name="maxDistance"/> of the origin.</summary>
    public SlidingDoor FindNearestGate(Vector2 origin, float maxDistance)
    {
        EnsureFresh();

        SlidingDoor nearest = null;
        float nearestSqr = maxDistance * maxDistance;
        for (int i = 0; i < gates.Count; i++)
        {
            SlidingDoor gate = gates[i];
            if (gate == null || gate.IsOpen)
                continue;

            float sqr = ((Vector2)gate.transform.position - origin).sqrMagnitude;
            if (sqr <= nearestSqr)
            {
                nearestSqr = sqr;
                nearest = gate;
            }
        }

        return nearest;
    }

    /// <summary>Nearest component of the requested type within <paramref name="maxDistance"/>.</summary>
    public T FindNearest<T>(Vector2 origin, float maxDistance) where T : Component
    {
        EnsureFresh();

        T nearest = null;
        float nearestSqr = maxDistance * maxDistance;
        for (int i = 0; i < contacts.Count; i++)
        {
            Collider2D contact = contacts[i];
            if (contact == null)
                continue;

            T candidate = contact.GetComponentInParent<T>();
            if (candidate == null)
                continue;

            float sqr = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            if (sqr <= nearestSqr)
            {
                nearestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}
