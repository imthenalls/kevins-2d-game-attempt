using Game.Core;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads starting NPC item ownership from StreamingAssets/npc_inventories.json and
/// seeds each matching NpcController inventory once per runtime session.
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

    private readonly Dictionary<string, NpcInventoryEntry> entries =
        new Dictionary<string, NpcInventoryEntry>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> initializedNpcIds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Seeds all configured, not-yet-initialized NPCs in the active scene.</summary>
    public void ApplyToLoadedScene()
    {
        if (entries.Count == 0 || ItemDatabase.Instance == null)
            return;

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
            if (initializedNpcIds.Contains(pair.Key))
                continue;

            if (pair.Value.Count != 1)
            {
                Debug.LogError(
                    $"[NpcInventoryDatabase] Found {pair.Value.Count} NPCs with configured id " +
                    $"'{pair.Key}'. NPC ids must be unique; starting inventory was not loaded.");
                continue;
            }

            SeedInventory(pair.Value[0], entries[pair.Key]);
            initializedNpcIds.Add(pair.Key);
        }
    }

    /// <summary>Clears runtime initialization tracking for a new-game flow.</summary>
    public void ResetSessionState() => initializedNpcIds.Clear();

    private void SeedInventory(NpcController npc, NpcInventoryEntry entry)
    {
        InventoryModel inventory = npc.EnsureInventory();
        if (entry.items == null)
            return;

        foreach (NpcInventoryItemEntry ownedItem in entry.items)
        {
            if (ownedItem == null || string.IsNullOrWhiteSpace(ownedItem.itemId) || ownedItem.quantity <= 0)
            {
                Debug.LogWarning($"[NpcInventoryDatabase] NPC '{entry.npcId}' has an invalid item entry.");
                continue;
            }

            if (!ItemDatabase.Instance.TryGet(ownedItem.itemId, out ItemData item))
            {
                Debug.LogWarning(
                    $"[NpcInventoryDatabase] NPC '{entry.npcId}' references unknown item " +
                    $"'{ownedItem.itemId}'.");
                continue;
            }

            int leftover = inventory.AddItem(item, ownedItem.quantity);
            if (leftover > 0)
            {
                Debug.LogWarning(
                    $"[NpcInventoryDatabase] NPC '{entry.npcId}' inventory could not fit " +
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

        foreach (NpcInventoryEntry entry in root.npcInventories)
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
    public NpcInventoryEntry[] npcInventories;
}

/// <summary>Starting inventory assigned to one stable NPC id. Unity setup: none.</summary>
[Serializable]
internal sealed class NpcInventoryEntry
{
    public string npcId;
    public NpcInventoryItemEntry[] items;
}

/// <summary>One item stack in an NPC starting inventory. Unity setup: none.</summary>
[Serializable]
internal sealed class NpcInventoryItemEntry
{
    public string itemId;
    public int quantity = 1;
}
