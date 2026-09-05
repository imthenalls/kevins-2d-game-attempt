using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads enemy-owned loot from StreamingAssets/enemy_loot.json before combat and creates
/// a separate interactable loot pile from that same inventory when the enemy dies.
///
/// Unity setup: none. NpcController calls PrepareInventory for Enemy NPCs, and
/// CombatReceiver calls Spawn on death. Loot piles are created at runtime with their own
/// SpriteRenderer, trigger collider, and RuntimeEnemyLootPile interaction component.
///
/// Runtime API:
///   PrepareInventory(NpcController) loads the matching NPC id's items into its inventory.
///   Spawn(NpcController) creates the world pile and transfers ownership of that inventory.
/// </summary>
public static class EnemyLootDrop
{
    private static readonly Dictionary<string, EnemyLootDefinition> Definitions =
        new Dictionary<string, EnemyLootDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<NpcController> PreparedEnemies = new HashSet<NpcController>();
    private static bool loaded;
    private static Sprite pileSprite;

    /// <summary>Seeds the matching enemy's actual InventoryModel once per spawned NPC.</summary>
    public static void PrepareInventory(NpcController npc)
    {
        if (npc == null || npc.NpcType != NpcType.Enemy || PreparedEnemies.Contains(npc))
            return;

        EnsureDefinitionsLoaded();
        PreparedEnemies.Add(npc);

        if (!Definitions.TryGetValue(npc.NpcId, out EnemyLootDefinition definition))
            return;

        InventoryModel inventory = npc.EnsureInventory();
        if (definition.items == null || ItemDatabase.Instance == null)
            return;

        foreach (EnemyLootItemDefinition entry in definition.items)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.itemId))
                continue;

            if (!ItemDatabase.Instance.TryGet(entry.itemId, out ItemData item))
            {
                Debug.LogWarning(
                    $"[EnemyLootDrop] Enemy '{npc.NpcId}' references unknown item '{entry.itemId}'.");
                continue;
            }

            int minimum = Mathf.Max(0, entry.minQuantity);
            int maximum = Mathf.Max(minimum, entry.maxQuantity);
            int quantity = UnityEngine.Random.Range(minimum, maximum + 1);
            if (quantity <= 0)
                continue;

            int leftover = inventory.AddItem(item, quantity);
            if (leftover > 0)
            {
                Debug.LogWarning(
                    $"[EnemyLootDrop] '{npc.DisplayName}' could not hold {leftover}x '{entry.itemId}'.");
            }
        }
    }

    /// <summary>Creates a separate world loot pile that references the defeated NPC's inventory.</summary>
    public static void Spawn(NpcController npc)
    {
        if (npc == null || npc.Inventory == null || IsEmpty(npc.Inventory))
            return;

        EnsureDefinitionsLoaded();
        string dropName = Definitions.TryGetValue(npc.NpcId, out EnemyLootDefinition definition)
            && !string.IsNullOrWhiteSpace(definition.dropName)
                ? definition.dropName
                : npc.DisplayName + " Loot";

        var dropObject = new GameObject(dropName);
        dropObject.transform.position = npc.transform.position;

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        dropObject.layer = interactableLayer >= 0 ? interactableLayer : npc.gameObject.layer;

        var renderer = dropObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetPileSprite();
        renderer.color = Color.white;
        CopySortingFromNpc(npc, renderer);

        var collider = dropObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.55f;

        var pile = dropObject.AddComponent<RuntimeEnemyLootPile>();
        pile.Initialize(npc.Inventory, dropName, 2.25f);
    }

    private static bool IsEmpty(InventoryModel inventory)
    {
        for (int i = 0; i < inventory.SlotCount; i++)
            if (!inventory.GetSlot(i).IsEmpty)
                return false;
        return true;
    }

    private static void EnsureDefinitionsLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        string path = Path.Combine(Application.streamingAssetsPath, "enemy_loot.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[EnemyLootDrop] enemy_loot.json not found at: " + path);
            return;
        }

        try
        {
            EnemyLootDatabaseJson root = JsonUtility.FromJson<EnemyLootDatabaseJson>(File.ReadAllText(path));
            if (root?.enemyLoot == null)
                return;

            foreach (EnemyLootDefinition definition in root.enemyLoot)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.npcId))
                    continue;

                Definitions[definition.npcId] = definition;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[EnemyLootDrop] Failed to parse enemy_loot.json: {exception.Message}");
        }
    }

    private static void CopySortingFromNpc(NpcController npc, SpriteRenderer target)
    {
        SpriteRenderer[] renderers = npc.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers.Length == 0)
            return;

        target.sortingLayerID = renderers[0].sortingLayerID;
        int highestOrder = renderers[0].sortingOrder;
        foreach (SpriteRenderer renderer in renderers)
            highestOrder = Mathf.Max(highestOrder, renderer.sortingOrder);
        target.sortingOrder = highestOrder + 1;
    }

    private static Sprite GetPileSprite()
    {
        if (pileSprite != null)
            return pileSprite;

        const int width = 32;
        const int height = 28;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "enemy_loot_pile_generated",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width * height];

        for (int y = 4; y <= 19; y++)
        for (int x = 6; x <= 25; x++)
        {
            float normalizedX = (x - 15.5f) / 10f;
            float normalizedY = (y - 11.5f) / 8f;
            if (normalizedX * normalizedX + normalizedY * normalizedY <= 1f)
                pixels[y * width + x] = y < 7
                    ? new Color32(108, 58, 25, 255)
                    : new Color32(151, 84, 35, 255);
        }

        for (int x = 11; x <= 20; x++)
            pixels[20 * width + x] = new Color32(224, 178, 47, 255);
        for (int y = 21; y <= 24; y++)
        for (int x = 13; x <= 18; x++)
            pixels[y * width + x] = new Color32(151, 84, 35, 255);

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        pileSprite = Sprite.Create(
            texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.35f), 28f);
        pileSprite.name = "Enemy Loot Pile";
        return pileSprite;
    }
}

/// <summary>
/// Makes a runtime-created enemy loot pile discoverable by PlayerInteractionController and
/// opens LootContainerUI on interaction. It destroys the pile after every item is taken.
///
/// Unity setup: do not add manually. EnemyLootDrop.Spawn creates this component alongside
/// a SpriteRenderer and CircleCollider2D and supplies its inventory, name, and range.
///
/// Runtime API:
///   Initialize(InventoryModel, string, float) binds the owned loot inventory.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class RuntimeEnemyLootPile : MonoBehaviour, IInteractable
{
    private InventoryModel inventory;
    private string displayName;
    private float interactionRange;
    private bool emptied;

    /// <summary>Binds the inventory that was owned by the defeated enemy.</summary>
    public void Initialize(InventoryModel source, string shownName, float range)
    {
        inventory = source;
        displayName = shownName;
        interactionRange = Mathf.Max(0.25f, range);
        if (inventory != null)
            inventory.OnChanged += HandleInventoryChanged;
    }

    public bool CanInteract(Vector3 worldPosition) =>
        !emptied && inventory != null
        && Vector2.Distance(transform.position, worldPosition) <= interactionRange;

    public string GetDisplayName() => displayName;

    public bool TryGetCurrentLine(out string line)
    {
        line = string.Empty;
        return false;
    }

    public void Advance() { }

    public void EndInteraction(GameObject interactor)
    {
        if (emptied || inventory == null)
            return;

        // Some scenes only have the core InventoryUI and no dedicated loot panel.
        // In that case E still works by collecting every stack that fits.
        if (!LootContainerUI.Show(inventory, displayName))
            TakeAllDirectly(interactor);
    }

    private void TakeAllDirectly(GameObject interactor)
    {
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (slot.IsEmpty)
                continue;

            ItemData item = slot.item;
            int taken = InventoryHelper.GiveItem(item, slot.quantity, interactor);
            if (taken > 0)
                inventory.RemoveItem(item, taken);
        }
    }

    private void HandleInventoryChanged()
    {
        if (inventory == null)
            return;

        for (int i = 0; i < inventory.SlotCount; i++)
            if (!inventory.GetSlot(i).IsEmpty)
                return;

        emptied = true;
        LootContainerUI.Hide();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnChanged -= HandleInventoryChanged;
    }
}

/// <summary>Root DTO for enemy_loot.json. Unity setup: none; created by JsonUtility.</summary>
[Serializable]
internal sealed class EnemyLootDatabaseJson
{
    public int version = 1;
    public EnemyLootDefinition[] enemyLoot;
}

/// <summary>One NPC-id loot definition loaded from JSON. Unity setup: none.</summary>
[Serializable]
internal sealed class EnemyLootDefinition
{
    public string npcId;
    public string dropName;
    public EnemyLootItemDefinition[] items;
}

/// <summary>One randomized item stack in an enemy loot definition. Unity setup: none.</summary>
[Serializable]
internal sealed class EnemyLootItemDefinition
{
    public string itemId;
    public int minQuantity = 1;
    public int maxQuantity = 1;
}
