using Game.Core;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads starting NPC item ownership from StreamingAssets/npc_inventories.json and seeds each
/// matching NpcController inventory. The one-time "seed or not" decision and the "already
/// initialized" state live in Game.Core (NpcInventoryInitializationService, owned by GameSession);
/// this component only parses the JSON, resolves ItemData, finds the scene NPCs, and applies the seed.
///
/// Unity setup: none. A persistent instance is created automatically before scene load.
/// NPCs must have unique NpcController.NpcId values matching the JSON entries. Inventory
/// and Wallet components/data are created automatically through NpcController.EnsureInventory.
/// Saved NPC inventories override this starting data when SaveManager restores a save.
///
/// Runtime API:
///   NpcInventoryDatabase.Instance.ApplyToLoadedScene() reapplies uninitialized entries.
///   ResetSessionState() allows a new-game flow to seed starting inventories again.
/// </summary>
[DisallowMultipleComponent]
public class NpcInventoryDatabase : MonoBehaviour
{
    public static NpcInventoryDatabase Instance { get; private set; }

    private readonly Dictionary<string, NpcStartingInventory> entries =
        new Dictionary<string, NpcStartingInventory>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
            return;

        var databaseObject = new GameObject("NPC Inventory Database");
        databaseObject.AddComponent<NpcInventoryDatabase>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFromJson();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void HandleSceneLoaded(Scene _, LoadSceneMode __) => ApplyToLoadedScene();

    // Fallback seed for scenes restored without a sceneLoaded event (for example an Enter Play Mode
    // or domain-reload backup scene), so NPC keys still exist after a script reload.
    private void Start() => ApplyToLoadedScene();

    /// <summary>Seeds all configured, not-yet-initialized NPCs in the active scene.</summary>
    public void ApplyToLoadedScene()
    {
        if (entries.Count == 0 || ItemDatabase.Instance == null)
            return;

        GameSessionHost.EnsureExists();
        NpcInventoryInitializationService initialization = GameSessionHost.Session?.NpcInventories;

        NpcController[] sceneNpcs = FindObjectsByType<NpcController>(FindObjectsInactive.Exclude);
        var matches = new Dictionary<string, List<NpcController>>(StringComparer.OrdinalIgnoreCase);

        foreach (NpcController npc in sceneNpcs)
        {
            if (npc == null || !entries.ContainsKey(npc.NpcId))
                continue;

            if (!matches.TryGetValue(npc.NpcId, out List<NpcController> list))
            {
                list = new List<NpcController>();
                matches[npc.NpcId] = list;
            }
            list.Add(npc);
        }

        foreach (var pair in matches)
        {
            if (pair.Value.Count != 1)
            {
                Debug.LogError(
                    $"[NpcInventoryDatabase] Found {pair.Value.Count} NPCs with configured id " +
                    $"'{pair.Key}'. NPC ids must be unique; starting inventory was not loaded.");
                continue;
            }

            NpcController npc = pair.Value[0];

            // The seed-once rule lives in Core and consults the inventory's initialized flag, not the
            // item count, so a legitimately emptied NPC is never reseeded on a scene reload.
            if (initialization != null && !initialization.TryClaimSeed(pair.Key, npc.Inventory))
                continue;

            SeedInventory(npc, entries[pair.Key], initialization);
        }
    }

    /// <summary>Clears runtime initialization tracking for a new-game flow.</summary>
    public void ResetSessionState() => GameSessionHost.Session?.NpcInventories.ResetSession();

    private void SeedInventory(
        NpcController npc,
        NpcStartingInventory definition,
        NpcInventoryInitializationService initialization)
    {
        InventoryModel inventory = npc.EnsureInventory();
        inventory.MarkInitialized();
        initialization?.MarkInitialized(npc.NpcId, inventory);

        if (definition.items == null)
            return;

        foreach (NpcStartingItem ownedItem in definition.items)
        {
            if (ownedItem == null || string.IsNullOrWhiteSpace(ownedItem.itemId) || ownedItem.quantity <= 0)
            {
                Debug.LogWarning($"[NpcInventoryDatabase] NPC '{definition.npcId}' has an invalid item entry.");
                continue;
            }

            if (!ItemDatabase.Instance.TryGet(ownedItem.itemId, out ItemData item))
            {
                Debug.LogWarning(
                    $"[NpcInventoryDatabase] NPC '{definition.npcId}' references unknown item " +
                    $"'{ownedItem.itemId}'.");
                continue;
            }

            int leftover = inventory.AddItem(item, ownedItem.quantity);
            if (leftover > 0)
            {
                Debug.LogWarning(
                    $"[NpcInventoryDatabase] NPC '{definition.npcId}' inventory could not fit " +
                    $"{leftover}x '{ownedItem.itemId}'.");
            }
        }
    }

    private void LoadFromJson()
    {
        entries.Clear();
        string path = Path.Combine(Application.streamingAssetsPath, "npc_inventories.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[NpcInventoryDatabase] npc_inventories.json not found at: " + path);
            return;
        }

        NpcInventoryDatabaseJson root;
        try
        {
            root = JsonUtility.FromJson<NpcInventoryDatabaseJson>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NpcInventoryDatabase] Failed to parse npc_inventories.json: {exception.Message}");
            return;
        }

        if (root?.npcInventories == null)
            return;

        foreach (NpcStartingInventory entry in root.npcInventories)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.npcId))
            {
                Debug.LogWarning("[NpcInventoryDatabase] Skipping entry with an empty npcId.");
                continue;
            }

            if (entries.ContainsKey(entry.npcId))
            {
                Debug.LogError($"[NpcInventoryDatabase] Duplicate JSON entry for npcId '{entry.npcId}'.");
                continue;
            }

            entries[entry.npcId] = entry;
        }

        Debug.Log($"[NpcInventoryDatabase] Loaded {entries.Count} NPC inventory definitions.");
    }
}

/// <summary>Root JSON data for NPC starting inventories. Unity setup: none.</summary>
[Serializable]
internal sealed class NpcInventoryDatabaseJson
{
    public int version = 1;
    public NpcStartingInventory[] npcInventories;
}
