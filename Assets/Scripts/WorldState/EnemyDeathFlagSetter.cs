using UnityEngine;

/// <summary>
/// Automatically sets a world state flag when this enemy is killed.
/// Attach to any boss or important enemy whose defeat should permanently change the world.
///
/// Key resolution order (first non-empty wins):
///   1. Flag Key asset (WorldStateKey ScriptableObject).
///   2. Raw Key string field.
///   3. Auto-generated key: "Enemy.{NpcId}.Defeated" (requires NpcController on same object).
///
/// Unity setup:
///   1. Add to an enemy GameObject that has CombatReceiver (required component).
///   2. Either:
///        a. Leave Auto Generate Key enabled — key is auto-derived from NpcController.NpcId.
///           e.g. NpcId "cave_spider" → flag "Enemy.cave_spider.Defeated"
///        b. Assign a WorldStateKey asset for a custom, designer-defined key.
///        c. Type a raw string in Raw Key as a quick fallback.
///
/// Pairs naturally with:
///   WorldStateDestroyer — removes the dead boss on next load if flag is already set.
///   WorldStateActivator — reveals post-kill content (treasure room, NPC, etc.).
///   WorldStateSpawner   — despawns enemy group after boss is defeated.
///
/// Requires CombatReceiver on the same GameObject (enforced by RequireComponent).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CombatReceiver))]
public class EnemyDeathFlagSetter : MonoBehaviour
{
    [Header("Flag Key")]
    [Tooltip("WorldStateKey asset to set on death. Highest priority.")]
    [SerializeField] private WorldStateKey flagKey;
    [Tooltip("Fallback raw string key. Used only if Flag Key asset is not assigned.")]
    [SerializeField] private string rawKey;
    [Tooltip("Auto-derive the flag key as 'Enemy.<NpcId>.Defeated'. Ignored when an asset or raw key is provided.")]
    [SerializeField] private bool autoGenerateKey = true;

    private CombatReceiver _combatReceiver;
    private NpcController  _npcController;

    private void Awake()
    {
        _combatReceiver = GetComponent<CombatReceiver>();
        _npcController  = GetComponent<NpcController>();
        _combatReceiver.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (_combatReceiver != null)
            _combatReceiver.OnDeath -= HandleDeath;
    }

    private void HandleDeath(CombatReceiver _)
    {
        if (WorldStateManager.Instance == null)
        {
            Debug.LogWarning("[EnemyDeathFlagSetter] WorldStateManager not found. Flag will not be set.", this);
            return;
        }

        string key = ResolveKey();
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning("[EnemyDeathFlagSetter] No flag key configured. Add a WorldStateKey asset, "
                           + "fill Raw Key, or enable Auto Generate Key.", this);
            return;
        }

        WorldStateManager.Instance.SetFlag(key);
        Debug.Log($"[EnemyDeathFlagSetter] Set world state flag '{key}'.");
    }

    private string ResolveKey()
    {
        if (flagKey != null) return (string)flagKey;
        if (!string.IsNullOrEmpty(rawKey)) return rawKey;
        if (autoGenerateKey && _npcController != null)
            return $"Enemy.{_npcController.NpcId}.Defeated";
        return string.Empty;
    }
}
