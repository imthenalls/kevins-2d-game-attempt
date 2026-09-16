using Game.Core;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Singleton that loads items.json from StreamingAssets and provides lookup by itemId.
/// Instantiates ItemData objects at runtime — no ScriptableObject assets needed per item.
///
/// Setup: none required. A persistent ItemDatabase is created automatically before
/// the first scene loads. A manually placed instance is still supported.
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
        {
            return;
        }

        var databaseObject = new GameObject("Item Database");
        databaseObject.AddComponent<ItemDatabase>();
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
            data.scope        = Enum.TryParse(entry.worldScope, true, out ItemScope scope)
                ? scope
                : ItemScope.Shared;

            if (!string.IsNullOrEmpty(entry.iconPath))
            {
                data.icon = Resources.Load<Sprite>(entry.iconPath);
                if (data.icon == null)
                {
                    Sprite[] sprites = Resources.LoadAll<Sprite>(entry.iconPath);
                    if (sprites.Length > 0)
                    {
                        data.icon = sprites[0];
                    }
                }
            }

            if (data.icon == null)
                data.icon = CreateBuiltInLootIcon(data.itemId);

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

    private static Sprite CreateBuiltInLootIcon(string itemId)
    {
        if (itemId != "gold_coin" && itemId != "broken_sword" && itemId != "golden_key")
            return null;

        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = itemId + "_generated_icon",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];

        if (itemId == "gold_coin")
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = x - 16;
                int dy = y - 16;
                int distance = dx * dx + dy * dy;
                if (distance <= 121)
                    pixels[y * size + x] = distance >= 90
                        ? new Color32(151, 91, 10, 255)
                        : new Color32(245, 193, 38, 255);
            }

            for (int y = 11; y <= 20; y++)
                pixels[y * size + 16] = new Color32(255, 235, 116, 255);
        }
        else if (itemId == "broken_sword")
        {
            DrawThickLine(pixels, size, 7, 6, 14, 13, new Color32(117, 76, 42, 255), 2);
            DrawThickLine(pixels, size, 11, 10, 17, 16, new Color32(224, 229, 231, 255), 2);
            DrawThickLine(pixels, size, 20, 19, 27, 26, new Color32(224, 229, 231, 255), 2);
            DrawThickLine(pixels, size, 7, 13, 13, 7, new Color32(174, 125, 55, 255), 1);
        }
        else
        {
            Color32 gold = new Color32(240, 185, 38, 255);
            Color32 highlight = new Color32(255, 225, 102, 255);
            DrawThickLine(pixels, size, 13, 16, 27, 16, gold, 2);
            DrawThickLine(pixels, size, 22, 16, 22, 11, gold, 1);
            DrawThickLine(pixels, size, 26, 16, 26, 12, gold, 1);
            for (int y = 8; y <= 24; y++)
            for (int x = 2; x <= 18; x++)
            {
                int dx = x - 10;
                int dy = y - 16;
                int distance = dx * dx + dy * dy;
                if (distance <= 49 && distance >= 16)
                    pixels[y * size + x] = distance >= 36 ? gold : highlight;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static void DrawThickLine(
        Color32[] pixels, int size, int startX, int startY, int endX, int endY,
        Color32 color, int radius)
    {
        int steps = Mathf.Max(Mathf.Abs(endX - startX), Mathf.Abs(endY - startY));
        for (int step = 0; step <= steps; step++)
        {
            float t = steps == 0 ? 0f : step / (float)steps;
            int centerX = Mathf.RoundToInt(Mathf.Lerp(startX, endX, t));
            int centerY = Mathf.RoundToInt(Mathf.Lerp(startY, endY, t));
            for (int y = centerY - radius; y <= centerY + radius; y++)
            for (int x = centerX - radius; x <= centerX + radius; x++)
                if (x >= 0 && x < size && y >= 0 && y < size)
                    pixels[y * size + x] = color;
        }
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
    public string   worldScope;   // Shared | WorldA | WorldB; missing defaults to Shared
    public string   iconPath;     // optional Resources path for the icon sprite
    // equipment fields (only read when type == Equipment)
    public string   equipSlot;    // Weapon | Armor | Accessory
    public int      bonusMaxHp;
    public int      bonusMaxMp;
    public int      bonusAttack;
    public int      bonusDefense;
}
