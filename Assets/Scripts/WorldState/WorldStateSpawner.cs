using UnityEngine;

/// <summary>
/// Instantiates a prefab when a world state flag is set (or absent, if configured).
/// Use to spawn NPCs, props, quest triggers, or any scene object after a world event.
///
/// The spawner checks its condition on Start and again whenever the watched flag changes.
/// Only one instance of the prefab is spawned; subsequent condition-true evaluations
/// are ignored if an instance already exists.
///
/// Unity setup:
///   1. Add to an empty scene GameObject placed at the desired spawn origin.
///   2. Assign Flag Key and Prefab.
///   3. Optionally assign Spawn Point to use a different position/rotation than this transform.
///   4. Enable Spawn When Flag Absent to spawn while the flag is NOT set.
///   5. Enable Destroy On Flag Cleared to remove the instance when the flag clears.
///
/// Examples:
///   Merchant.Rescued set → merchant NPC prefab appears in the town square.
///   BanditCamp.Cleared set + Spawn When Flag Absent OFF → enemy spawn trigger disabled.
///   Village.InDanger set + Spawn When Flag Absent ON → enemies appear while unresolved.
/// </summary>
[DisallowMultipleComponent]
public class WorldStateSpawner : MonoBehaviour
{
    [Header("Condition")]
    [Tooltip("WorldStateKey asset. Falls back to Raw Key if unassigned.")]
    [SerializeField] private WorldStateKey flagKey;
    [Tooltip("Fallback raw string key.")]
    [SerializeField] private string rawKey;
    [Tooltip("When enabled, the prefab spawns while the flag is NOT set (inverts the condition).")]
    [SerializeField] private bool spawnWhenFlagAbsent;

    [Header("Spawn")]
    [Tooltip("The prefab to instantiate when the condition is met.")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Override spawn position and rotation. Defaults to this transform if blank.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Destroy the spawned instance when the condition becomes false again.")]
    [SerializeField] private bool destroyOnConditionFalse;

    private GameObject _spawned;
    private string Key => flagKey != null ? (string)flagKey : rawKey;

    private void OnEnable()  => WorldStateManager.OnFlagChanged += HandleFlagChanged;
    private void OnDisable() => WorldStateManager.OnFlagChanged -= HandleFlagChanged;
    private void Start()     => Refresh();

    private void HandleFlagChanged(string changedKey)
    {
        if (changedKey == Key) Refresh();
    }

    private void Refresh()
    {
        if (string.IsNullOrEmpty(Key) || prefab == null) return;

        bool flagSet     = WorldStateManager.Instance != null && WorldStateManager.Instance.HasFlag(Key);
        bool shouldSpawn = spawnWhenFlagAbsent ? !flagSet : flagSet;

        if (shouldSpawn && _spawned == null)
        {
            Transform origin = spawnPoint != null ? spawnPoint : transform;
            _spawned = Instantiate(prefab, origin.position, origin.rotation);
        }
        else if (!shouldSpawn && _spawned != null && destroyOnConditionFalse)
        {
            Destroy(_spawned);
            _spawned = null;
        }
    }
}
