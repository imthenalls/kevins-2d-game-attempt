using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// A receiving-only portal destination. It supplies a stable <see cref="PortalId"/> and an arrival
/// point so another portal can teleport a traveler here, but it has no trigger collider and no
/// outgoing route — a traveler can land on it and can never be sent back through it. Use this for a
/// one-way exit (for example, an underground corridor that drops the player into the town park).
///
/// Unlike <see cref="PortalTrigger3D"/> it never reacts to contact, so walking over the arrival
/// position is inert. The Generic component runs no Update/physics work.
///
/// Unity setup:
///   1. Add to an otherwise empty GameObject (no Collider) at the arrival position.
///   2. Set Portal Id to the id the source portal targets.
///   3. Optionally assign an Exit Point child for an exact offset; otherwise the object's own
///      transform is the arrival position.
///
/// Runtime API: read by PortalManager (TryFindPortal / TryTeleportToPortal). Never a travel source.
/// </summary>
[DisallowMultipleComponent]
public class PortalArrival3D : MonoBehaviour, IPortalRoute
{
    [Header("Identity")]
    [Tooltip("Stable id a source portal targets as its Destination Portal Id.")]
    [SerializeField] private string portalId;

    [Header("Arrival")]
    [Tooltip("Exact arrival position. Defaults to this object's transform when left empty.")]
    [SerializeField] private Transform exitPoint;

    [Header("Config (Game.Data)")]
    [SerializeField] private PortalTriggerConfig config = new PortalTriggerConfig();

    private readonly List<string> additionalIncomingSources = new List<string>();

    public string PortalId => portalId;
    public string DestinationScene => string.Empty;
    public string DestinationPortalId => string.Empty;
    public bool ChangesWorld => false;
    public WorldLayer DestinationWorld => WorldLayer.WorldA;
    public string RequiredUnlockFlag => string.Empty;
    public string RequiredKeyId => string.Empty;
    public bool IsArrivalOnly => true;
    public Transform ExitPoint => exitPoint != null ? exitPoint : transform;
    public List<string> AdditionalIncomingSources => additionalIncomingSources;
    public float TravelCooldown => config.TravelCooldown;
    public Vector3 ArrivalPosition => ExitPoint.position;
    public Component Self => this;

    public bool IsUnlocked() => true;

    public void BlockForSeconds(float seconds) { }

    private void OnValidate()
    {
        portalId = portalId != null ? portalId.Trim() : string.Empty;
    }
}
