using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Routing contract shared by 2D (<see cref="PortalTrigger2D"/>) and 3D (<see cref="PortalTrigger3D"/>)
/// portals, so <see cref="PortalManager"/> and other systems can route travel without knowing the
/// dimension. A portal component is the single source of truth for identity, destination, arrival
/// point and cooldown; this interface is just the read/act surface.
///
/// Unity setup: none. Implemented by portal components.
///
/// Runtime API: read by PortalManager and the portal map exporter.
/// </summary>
public interface IPortalRoute
{
    string PortalId { get; }
    string DestinationScene { get; }
    string DestinationPortalId { get; }
    bool ChangesWorld { get; }
    WorldLayer DestinationWorld { get; }
    string RequiredUnlockFlag { get; }

    /// <summary>Optional key item id a traveler must hold (via IKeyHolder) to use this route.</summary>
    string RequiredKeyId { get; }

    Transform ExitPoint { get; }
    Vector3 ArrivalPosition { get; }
    float TravelCooldown { get; }
    List<string> AdditionalIncomingSources { get; }

    bool IsUnlocked();
    void BlockForSeconds(float seconds);

    /// <summary>The owning component, for logging context and scene lookup.</summary>
    Component Self { get; }
}
