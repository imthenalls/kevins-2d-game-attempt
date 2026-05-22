using UnityEngine;

public enum ItemType { Consumable, Material, Equipment, Misc }

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

    /// <summary>True when this item's type is Equipment.</summary>
    public bool IsEquip => type == ItemType.Equipment;

    /// <summary>
    /// Unique and QuestItem flags force a stack size of 1.
    /// </summary>
    public bool IsStackable =>
        (flags & (ItemFlags.Unique | ItemFlags.QuestItem)) == 0 && maxStackSize > 1;
}
