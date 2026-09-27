using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Caches nearby world information for NPC behaviors in one place, so behaviors don't each run
/// their own physics queries. Scans on a fixed cadence and exposes the results as concrete lists.
///
/// The scan radius lives in Game.Core.NpcPerceptionConfig, and nearest-target/gate ranking lives in
/// Game.Core.NpcTargetSelection (open gates are ranked as ineligible); this component only samples
/// physics, resolves components, and forwards the ranking decision to Core.
///
/// Unity setup:
///   1. Add to an NPC GameObject that has NpcBehaviorManager (and a Rigidbody2D for movement).
///   2. Set Detection Layers to the physics layers worth scanning (interactables, player, NPCs).
///   3. Set Scan Radius on the Config (NpcPerceptionConfig) to the largest distance any behavior needs.
///   Behaviors read Player, Gates, and Contacts, or call FindNearestGate / FindNearest.
///
/// Runtime API:
///   Refresh() (called automatically each FixedUpdate), EnsureFresh(), Player, Gates, Contacts,
///   FindNearestGate(origin, maxDistance), FindNearest&lt;T&gt;(origin, maxDistance).
/// </summary>
[DisallowMultipleComponent]
public class NpcPerception : MonoBehaviour
{
    [Header("Config (Game.Data)")]
    [SerializeField] private NpcPerceptionConfig config = new NpcPerceptionConfig();

    [SerializeField] private LayerMask detectionLayers = ~0;

    private readonly List<Collider2D> buffer = new List<Collider2D>();
    private readonly List<Collider2D> contacts = new List<Collider2D>();
    private readonly List<SlidingDoor> gates = new List<SlidingDoor>();
    private readonly List<NpcTargetCandidate> candidates = new List<NpcTargetCandidate>();
    private readonly List<Component> candidateComponents = new List<Component>();
    private int lastRefreshFrame = -1;

    /// <summary>Nearest player transform seen in the last scan, if any.</summary>
    public Transform Player { get; private set; }

    /// <summary>All colliders seen in the last scan. Empty until the first Refresh.</summary>
    public List<Collider2D> Contacts => contacts;

    /// <summary>Distinct SlidingDoors seen in the last scan.</summary>
    public List<SlidingDoor> Gates => gates;

    private void FixedUpdate() => Refresh();

    private void OnValidate() => config.ScanRadius = Mathf.Max(0.5f, config.ScanRadius);

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
        int count = Physics2D.OverlapCircle(transform.position, config.ScanRadius, filter, buffer);
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
                PlayerControllerBase player = contact.GetComponentInParent<PlayerControllerBase>();
                if (player != null)
                    Player = player.transform;
            }
        }
    }

    /// <summary>Nearest closed gate within <paramref name="maxDistance"/> of the origin.</summary>
    public SlidingDoor FindNearestGate(Vector2 origin, float maxDistance)
    {
        EnsureFresh();

        candidates.Clear();
        for (int i = 0; i < gates.Count; i++)
        {
            SlidingDoor gate = gates[i];
            bool valid = gate != null && !gate.IsOpen;
            Vector3 position = valid ? gate.transform.position : Vector3.zero;
            candidates.Add(new NpcTargetCandidate(position.x, position.y, valid));
        }

        int index = NpcTargetSelection.NearestIndex(origin.x, origin.y, candidates, maxDistance);
        return index >= 0 ? gates[index] : null;
    }

    /// <summary>Nearest component of the requested type within <paramref name="maxDistance"/>.</summary>
    public T FindNearest<T>(Vector2 origin, float maxDistance) where T : Component
    {
        EnsureFresh();

        candidates.Clear();
        candidateComponents.Clear();
        for (int i = 0; i < contacts.Count; i++)
        {
            Collider2D contact = contacts[i];
            T candidate = contact != null ? contact.GetComponentInParent<T>() : null;
            candidateComponents.Add(candidate);
            Vector3 position = candidate != null ? candidate.transform.position : Vector3.zero;
            candidates.Add(new NpcTargetCandidate(position.x, position.y, candidate != null));
        }

        int index = NpcTargetSelection.NearestIndex(origin.x, origin.y, candidates, maxDistance);
        return index >= 0 ? candidateComponents[index] as T : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, config.ScanRadius);
    }
}
