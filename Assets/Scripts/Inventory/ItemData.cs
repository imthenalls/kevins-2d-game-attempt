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
/// ItemFlags:
///   Unique    — forces stack size to 1; only one copy can be held.
///   QuestItem — cannot be dropped or stacked; removed by quest actions.
///   KeyItem   — cannot be dropped.
///
/// Save/load: ItemData assets must be inside Assets/Resources/Items/ OR registered
/// with ItemDatabase so they can be looked up by itemId when a save file is loaded.
/// </summary>

[System.Flags]
public enum ItemFlags { None = 0, Unique = 1, QuestItem = 2, KeyItem = 4 }

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string itemName;
    [TextArea(2, 4)] public string description;
    public Sprite icon;

    [Header("Classification")]
    public ItemType type;
    public ItemFlags flags;

    [Header("Stacking")]
    [Min(1)] public int maxStackSize = 99;

    [Header("Economy")]
    [Min(0)] public int sellValue;

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
}
