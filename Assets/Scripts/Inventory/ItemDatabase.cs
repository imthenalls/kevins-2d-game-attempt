using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Singleton that loads items.json from StreamingAssets and provides lookup by itemId.
/// Instantiates ItemData objects at runtime — no ScriptableObject assets needed per item.
///
/// Setup: Add ItemDatabase to the same persistent bootstrap GameObject as SaveManager.
///
/// Usage:
///   ItemData sword = ItemDatabase.Instance.Get("iron_sword");
///   bool found     = ItemDatabase.Instance.TryGet("health_potion", out var potion);
/// </summary>
[DisallowMultipleComponent]
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    private readonly Dictionary<string, ItemData> _items = new();

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
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the ItemData for the given id, or null if not found.</summary>
    public ItemData Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        _items.TryGetValue(id, out var item);
        return item;
    }

    /// <summary>Returns true and sets <paramref name="item"/> if the id is registered.</summary>
    public bool TryGet(string id, out ItemData item) => _items.TryGetValue(id, out item);

    /// <summary>
    /// Manually register an ItemData (e.g. existing ScriptableObjects).
    /// Overwrites any existing entry with the same itemId.
    /// </summary>
    public void Register(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId)) return;
        _items[item.itemId] = item;
    }

    // ── JSON loading ──────────────────────────────────────────────────────────

    private void LoadFromJson()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "items.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[ItemDatabase] items.json not found at: " + path);
            return;
        }

        ItemDatabaseJson root;
        try
        {
            root = JsonUtility.FromJson<ItemDatabaseJson>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[ItemDatabase] Failed to parse items.json: {e.Message}");
            return;
        }

        if (root?.items == null) return;

        foreach (var entry in root.items)
        {
            if (string.IsNullOrEmpty(entry.id))
            {
                Debug.LogWarning("[ItemDatabase] Skipping item with empty id.");
                continue;
            }

            var data = ScriptableObject.CreateInstance<ItemData>();
            data.itemId       = entry.id;
            data.itemName     = entry.name;
            data.description  = entry.description;
            data.type         = Enum.TryParse(entry.type, out ItemType t) ? t : ItemType.Misc;
            data.maxStackSize = entry.maxStackSize > 0 ? entry.maxStackSize : 1;
            data.sellValue    = entry.sellValue;
            data.flags        = ParseFlags(entry.flags);

            if (!string.IsNullOrEmpty(entry.iconPath))
                data.icon = Resources.Load<Sprite>(entry.iconPath);

            if (data.IsEquip)
            {
                data.equipSlot   = Enum.TryParse(entry.equipSlot, out EquipSlotType es) ? es : EquipSlotType.Weapon;
                data.bonusMaxHp  = entry.bonusMaxHp;
                data.bonusMaxMp  = entry.bonusMaxMp;
                data.bonusAttack = entry.bonusAttack;
                data.bonusDefense= entry.bonusDefense;
            }

            _items[data.itemId] = data;
        }

        Debug.Log($"[ItemDatabase] Loaded {_items.Count} items from items.json.");
    }

    private static ItemFlags ParseFlags(string[] flags)
    {
        var result = ItemFlags.None;
        if (flags == null) return result;
        foreach (var f in flags)
            if (Enum.TryParse(f, out ItemFlags flag))
                result |= flag;
        return result;
    }
}

// ── JSON structure ────────────────────────────────────────────────────────────

[Serializable]
internal class ItemDatabaseJson
{
    public int         version;
    public ItemEntry[] items;
}

[Serializable]
internal class ItemEntry
{
    public string   id;
    public string   name;
    public string   description;
    public string   type;         // Consumable | Material | Equipment | Misc
    public bool     isEquip;      // informational — mirrors type == Equipment
    public int      maxStackSize;
    public int      sellValue;
    public string[] flags;        // Unique | QuestItem | KeyItem
    public string   iconPath;     // optional Resources path for the icon sprite
    // equipment fields (only read when type == Equipment)
    public string   equipSlot;    // Weapon | Armor | Accessory
    public int      bonusMaxHp;
    public int      bonusMaxMp;
    public int      bonusAttack;
    public int      bonusDefense;
}
