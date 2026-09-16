using Game.Core;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines a single item type: its name, icon, category,
/// stack rules, economy value, and special flags.
/// Create via right-click → Inventory → Item in the Project window.
///
/// Unity Inspector fields:
///   Identity  — itemId (must be unique), itemName, description, icon Sprite.
///   Classification — type (Consumable / Material / Equipment / Misc), ItemFlags.
///   Stacking  — maxStackSize (set to 1 for non-stackables).
///   Economy   — sellValue in gold.
///
/// Implements the engine-free <see cref="IItem"/> contract so inventories and the economy can
/// operate on items without a UnityEngine dependency.
///
/// Save/load: ItemData assets must be inside Assets/Resources/Items/ OR registered
/// with ItemDatabase so they can be looked up by itemId when a save file is loaded.
/// </summary>

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject, IItem
{
    [Header("Identity")]
    public string itemId;
    public string itemName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Classification")]
    public ItemType type;
    public ItemFlags flags;
    [Tooltip("Shared items travel between worlds. Other items stay in their world's inventory.")]
    public ItemScope scope = ItemScope.Shared;

    public bool IsAvailableInWorld(WorldLayer world) =>
        scope == ItemScope.Shared ||
        (scope == ItemScope.WorldA && world == WorldLayer.WorldA) ||
        (scope == ItemScope.WorldB && world == WorldLayer.WorldB);

    [Header("Stacking")]
    [Min(1)] public int maxStackSize = 99;

    [Header("Economy")]
    [Min(0)] public int sellValue;

    [Header("Use Effects (Consumable only)")]
    [Tooltip("HP restored when this item is used from the hotbar or inventory.")]
    [Min(0)] public int healHp;
    [Tooltip("MP restored when this item is used from the hotbar or inventory.")]
    [Min(0)] public int healMp;

    [Header("Equipment (only used when type = Equipment)")]
    public EquipSlotType equipSlot;
    [Min(0)] public int bonusMaxHp;
    [Min(0)] public int bonusMaxMp;
    [Min(0)] public int bonusAttack;
    [Min(0)] public int bonusDefense;

    /// <summary>True when this item's type is Equipment.</summary>
    public bool IsEquip => type == ItemType.Equipment;

    /// <summary>
    /// Unique and QuestItem flags force a stack size of 1.
    /// </summary>
    public bool IsStackable =>
        (flags & (ItemFlags.Unique | ItemFlags.QuestItem)) == 0 && maxStackSize > 1;

    // ── IItem (engine-free contract) ─────────────────────────────────────────
    public string ItemId => itemId;
    public string ItemName => itemName;
    public ItemType Type => type;
    public ItemFlags Flags => flags;
    public ItemScope Scope => scope;
    public int MaxStackSize => maxStackSize;
}
