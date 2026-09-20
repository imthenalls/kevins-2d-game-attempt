using UnityEngine;

/// <summary>
/// Marks where the player starts in a scene. One explicit anchor instead of relying on wherever the
/// authored player instance happens to sit.
///
/// Runtime precedence for the player's position:
///   1. a save load (`SaveManager`) - applied after the scene loads, so it wins;
///   2. a portal/travel position - applied after arrival, so it wins;
///   3. this spawn point - applied on scene load as the default first entry;
///   4. the authored player transform - only if the scene has no spawn point.
///
/// Unity setup:
///   1. Create an empty GameObject at the desired start position (on the floor).
///   2. Add PlayerSpawnPoint.
///   3. Leave Spawn Id as "default"; set others only if a scene needs named entrances.
///
/// Runtime API: none. GameBootstrap reads it on scene load.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Entry id: \"default\" for the normal start, or a named entrance.")]
    [SerializeField] private string spawnId = "default";

    public string SpawnId => spawnId;
    public Vector3 Position => transform.position;
}
