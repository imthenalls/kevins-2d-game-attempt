using UnityEngine;

/// <summary>
/// Destroys this GameObject permanently when a world state flag is set.
/// Use for scene objects that should disappear forever after a player action:
/// opened chests, smashed crates, defeated bosses, expired triggers, etc.
///
/// The destruction is permanent because the flag persists via SaveManager.
/// On the next scene load, if the flag is already set, this object destroys
/// itself immediately in Start before the player ever sees it.
///
/// Unity setup:
///   1. Add to any scene GameObject that should be permanently removable.
///   2. Assign Flag Key — drag a WorldStateKey asset, or type a Raw Key string.
///
/// Examples:
///   Enemy.CaveSpider.Defeated set → boss body GameObject disappears.
///   Chest.Dungeon1.Room3.Opened set → chest prop is removed.
///
/// Pairs naturally with EnemyDeathFlagSetter: place both on the same boss
/// to set the flag on death and immediately remove the corpse.
/// </summary>
[DisallowMultipleComponent]
public class WorldStateDestroyer : MonoBehaviour
{
    [Header("Condition")]
    [Tooltip("WorldStateKey asset. Falls back to Raw Key if unassigned.")]
    [SerializeField] private WorldStateKey flagKey;
    [Tooltip("Fallback raw string key.")]
    [SerializeField] private string rawKey;

    private string Key => flagKey != null ? (string)flagKey : rawKey;

    private void OnEnable()  => WorldStateManager.OnFlagChanged += HandleFlagChanged;
    private void OnDisable() => WorldStateManager.OnFlagChanged -= HandleFlagChanged;

    private void Start()
    {
        if (string.IsNullOrEmpty(Key)) return;
        if (WorldStateManager.Instance != null && WorldStateManager.Instance.HasFlag(Key))
            Destroy(gameObject);
    }

    private void HandleFlagChanged(string changedKey)
    {
        if (changedKey != Key) return;
        if (WorldStateManager.Instance != null && WorldStateManager.Instance.HasFlag(Key))
            Destroy(gameObject);
    }
}
