using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that handles writing and reading the save file.
/// Save data is written as JSON to Application.persistentDataPath/save.json.
///
/// What it saves:
///   - Active scene name and player position
///   - Player HP / MP
///   - All WorldStateManager facts
///   - Active quest instances (node positions + objective counts)
///   - Occupied inventory slots (by itemId)
///
/// Unity setup:
///   1. Add to a persistent bootstrap GameObject in your first scene
///      (alongside WorldStateManager, QuestManager, SceneLoader, ItemDatabase).
///   2. Call SaveManager.Instance.Save() from a pause menu or autosave trigger.
///   3. Call SaveManager.Instance.Load() from a main menu "Continue" button.
///   4. Use SaveManager.Instance.HasSave() to decide whether to show the button.
/// </summary>
[DisallowMultipleComponent]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string FileName = "save.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public bool HasSave() => File.Exists(SavePath);

    /// <summary>
    /// When false, Save() is a no-op. Set by SceneRulesManager for scenes where
    /// saving should be blocked (dungeons, boss arenas, cutscenes, etc.).
    /// </summary>
    public bool SaveEnabled { get; set; } = true;

    /// <summary>Collects all game state and writes it to disk as JSON.</summary>
    public void Save()
    {
        if (!SaveEnabled) return;
        var data = new SaveData();
        data.currentScene = SceneManager.GetActiveScene().name;

        // Player position + stats
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;

            if (player.TryGetComponent<EntityStats>(out var stats))
            {
                data.playerHp    = stats.Hp;
                data.playerMp    = stats.Mp;
                data.playerMaxHp = stats.MaxHp;
                data.playerMaxMp = stats.MaxMp;
            }
        }

        // World facts
        if (WorldStateManager.Instance != null)
        {
            foreach (var kv in WorldStateManager.Instance.GetSnapshot())
                data.worldFacts.Add(SerializeFact(kv.Key, kv.Value));
        }

        // Active quests
        if (QuestManager.Instance != null)
            data.activeQuests = QuestManager.Instance.GetSaveData();

        // Inventory — only occupied slots, keyed by itemId
        var inv = InventoryUI.Model;
        if (inv != null)
        {
            for (int i = 0; i < inv.SlotCount; i++)
            {
                var slot = inv.GetSlot(i);
                if (!slot.IsEmpty)
                    data.inventorySlots.Add(new InventorySlotEntry
                    {
                        slotIndex = i,
                        itemId    = slot.item.itemId,
                        quantity  = slot.quantity,
                    });
            }
        }

        // NPCs — position, stats (enemies), and inventory (vendors/loot)
        foreach (var npc in FindObjectsByType<NpcController>(FindObjectsSortMode.None))
        {
            var entry = new NpcSaveEntry
            {
                npcId = npc.NpcId,
                x     = npc.transform.position.x,
                y     = npc.transform.position.y,
            };

            if (npc.Stats != null)
            {
                entry.hasStats = true;
                entry.hp       = npc.Stats.Hp;
                entry.mp       = npc.Stats.Mp;
                entry.maxHp    = npc.Stats.MaxHp;
                entry.maxMp    = npc.Stats.MaxMp;
            }

            if (npc.Inventory != null)
            {
                for (int i = 0; i < npc.Inventory.SlotCount; i++)
                {
                    var slot = npc.Inventory.GetSlot(i);
                    if (!slot.IsEmpty)
                        entry.inventorySlots.Add(new InventorySlotEntry
                        {
                            slotIndex = i,
                            itemId    = slot.item.itemId,
                            quantity  = slot.quantity,
                        });
                }
            }

            data.npcStates.Add(entry);
        }
        // Hotbar
        if (HotbarUI.Model != null)
        {
            for (int i = 0; i < HotbarModel.SlotCount; i++)
            {
                var item = HotbarUI.Model.GetSlot(i);
                if (item != null)
                    data.hotbarSlots.Add(new HotbarEntry { slotIndex = i, itemId = item.itemId });
            }
        }
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"[SaveManager] Saved → {SavePath}");
    }

    /// <summary>
    /// Reads the save file, restores world-state and quest data immediately,
    /// then loads the saved scene and restores the player + inventory once it is ready.
    /// </summary>
    public void Load()
    {
        if (!HasSave())
        {
            Debug.LogWarning("[SaveManager] No save file found.");
            return;
        }

        var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));

        // Restore world facts before the scene loads so quest conditions are
        // already correct when newly-placed triggers evaluate on Awake/Start.
        if (WorldStateManager.Instance != null)
        {
            var snapshot = new Dictionary<string, object>();
            foreach (var fe in data.worldFacts)
                snapshot[fe.key] = DeserializeFact(fe);
            WorldStateManager.Instance.LoadSnapshot(snapshot);
        }

        // Restore quest instances (no onEnterActions re-fired)
        QuestManager.Instance?.LoadSaveData(data.activeQuests);

        // Everything that depends on scene objects is deferred until after load
        Action callback = null;
        callback = () =>
        {
            SceneLoader.Instance.OnLoadComplete -= callback;
            RestoreSceneState(data);
        };
        SceneLoader.Instance.OnLoadComplete += callback;
        SceneLoader.Instance.LoadScene(data.currentScene);
    }

    // ── Scene-dependent restore ───────────────────────────────────────────────

    private void RestoreSceneState(SaveData data)
    {
        // Player position + stats
        var player = FindFirstObjectByType<PlayerController2D>();
        if (player != null)
        {
            player.transform.position = new Vector3(data.playerX, data.playerY, 0f);

            if (player.TryGetComponent<EntityStats>(out var stats))
            {
                stats.Configure(data.playerMaxHp, data.playerMaxMp);
                stats.SetHp(data.playerHp);
                stats.SetMp(data.playerMp);
            }
        }

        // Inventory
        var inv = InventoryUI.Model;
        if (inv != null)
        {
            // Clear all slots first
            for (int i = 0; i < inv.SlotCount; i++)
                inv.GetSlot(i).Clear();

            // Restore occupied slots via ItemDatabase
            foreach (var entry in data.inventorySlots)
            {
                var item = ItemDatabase.Instance != null
                    ? ItemDatabase.Instance.Get(entry.itemId)
                    : null;

                if (item == null)
                {
                    Debug.LogWarning($"[SaveManager] Item not found in ItemDatabase: '{entry.itemId}'");
                    continue;
                }
                inv.GetSlot(entry.slotIndex).Set(item, entry.quantity);
            }

            inv.ForceRefresh();
        }

        // NPCs — restore position, stats, and inventory
        if (data.npcStates != null && data.npcStates.Count > 0)
        {
            // Build a lookup by npcId for O(1) access
            var npcLookup = new Dictionary<string, NpcController>();
            foreach (var npc in FindObjectsByType<NpcController>(FindObjectsSortMode.None))
                npcLookup[npc.NpcId] = npc;

            foreach (var entry in data.npcStates)
            {
                if (!npcLookup.TryGetValue(entry.npcId, out var npc))
                {
                    Debug.LogWarning($"[SaveManager] NPC not found in scene: '{entry.npcId}'");
                    continue;
                }

                npc.transform.position = new Vector3(entry.x, entry.y, 0f);

                if (entry.hasStats && npc.Stats != null)
                {
                    npc.Stats.Configure(entry.maxHp, entry.maxMp);
                    npc.Stats.SetHp(entry.hp);
                    npc.Stats.SetMp(entry.mp);
                }

                if (entry.inventorySlots != null && entry.inventorySlots.Count > 0 && npc.Inventory != null)
                {
                    for (int i = 0; i < npc.Inventory.SlotCount; i++)
                        npc.Inventory.GetSlot(i).Clear();

                    foreach (var slotEntry in entry.inventorySlots)
                    {
                        var item = ItemDatabase.Instance != null
                            ? ItemDatabase.Instance.Get(slotEntry.itemId)
                            : null;

                        if (item == null)
                        {
                            Debug.LogWarning($"[SaveManager] NPC '{entry.npcId}': item not found '{slotEntry.itemId}'");
                            continue;
                        }
                        npc.Inventory.GetSlot(slotEntry.slotIndex).Set(item, slotEntry.quantity);
                    }
                    npc.Inventory.ForceRefresh();
                }
            }
        }

        // Hotbar
        if (HotbarUI.Model != null && data.hotbarSlots != null)
        {
            for (int i = 0; i < HotbarModel.SlotCount; i++)
                HotbarUI.ClearSlot(i);

            foreach (var entry in data.hotbarSlots)
            {
                var item = ItemDatabase.Instance?.Get(entry.itemId);
                if (item != null)
                    HotbarUI.AssignSlot(entry.slotIndex, item);
            }
        }

        Debug.Log("[SaveManager] Scene state restored.");
    }

    // ── Serialization helpers ─────────────────────────────────────────────────

    private static FactEntry SerializeFact(string key, object value)
    {
        return value switch
        {
            bool  b => new FactEntry { key = key, value = b.ToString(), type = "bool" },
            int   i => new FactEntry { key = key, value = i.ToString(), type = "int" },
            float f => new FactEntry { key = key, value = f.ToString(System.Globalization.CultureInfo.InvariantCulture), type = "float" },
            _       => new FactEntry { key = key, value = value?.ToString() ?? "", type = "string" },
        };
    }

    private static object DeserializeFact(FactEntry fe)
    {
        return fe.type switch
        {
            "bool"  => bool.Parse(fe.value),
            "int"   => int.Parse(fe.value),
            "float" => float.Parse(fe.value, System.Globalization.CultureInfo.InvariantCulture),
            _       => fe.value,
        };
    }
}
