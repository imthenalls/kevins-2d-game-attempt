using UnityEngine;

/// <summary>
/// Marks a world-space position as a named arrival point for PortalManager teleportation.
/// After loading a new scene, PortalManager searches for the PortalSpawnPoint whose
/// spawnId matches the portal destination's spawnId and moves the traveler there.
///
/// Unity setup:
///   1. Add to an empty GameObject positioned exactly where travelers should arrive.
///   2. Set Spawn Id to a unique string (e.g. "village_south_gate").
///   3. Use the same string in the portal's destination.spawnId in portals.json.
///   One SpawnPoint per id is enough; multiple portals can reference the same point.
/// </summary>
public class PortalSpawnPoint : MonoBehaviour
{
    [SerializeField] private string spawnId;

    public string SpawnId => spawnId;
}
