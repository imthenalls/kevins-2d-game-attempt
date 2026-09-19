namespace Game.Core
{
    /// <summary>
    /// Engine-free description of an item that inventory and economy logic depends on. The Unity
    /// <c>ItemData</c> ScriptableObject implements this interface, so the domain never holds a
    /// UnityEngine reference while authored assets still carry icons and inspector data.
    ///
    /// Unity setup: none. Implemented by ItemData; consumed by InventoryModel and TradeService.
    /// </summary>
    public interface IItem
    {
        /// <summary>Stable, unique id (matches ItemData.itemId and the save data).</summary>
        string ItemId { get; }

        /// <summary>Display name used for sorting.</summary>
        string ItemName { get; }

        /// <summary>Broad category (used for sorting and filtering).</summary>
        ItemType Type { get; }

        /// <summary>Special behaviours (Unique / QuestItem / KeyItem).</summary>
        ItemFlags Flags { get; }

        /// <summary>Which world(s) this item may exist in.</summary>
        ItemScope Scope { get; }

        /// <summary>Maximum quantity per slot.</summary>
        int MaxStackSize { get; }

        /// <summary>True when more than one can share a slot.</summary>
        bool IsStackable { get; }

        /// <summary>True when this item is equipment.</summary>
        bool IsEquip { get; }

        /// <summary>Equipment slot this item belongs to (only meaningful when IsEquip).</summary>
        EquipSlotType EquipSlot { get; }
    }
}
