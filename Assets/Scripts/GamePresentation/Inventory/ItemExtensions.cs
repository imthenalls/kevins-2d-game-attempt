using Game.Core;

/// <summary>
/// Bridges the engine-free <see cref="IItem"/> contract back to the Unity <see cref="ItemData"/>
/// asset. Presentation-only data (icon Sprite, equipment slot, use effects) deliberately stays on
/// ItemData and is reached through this cast instead of leaking UnityEngine into Game.Data.
///
/// Unity setup: none — static helper.
/// </summary>
public static class ItemExtensions
{
    /// <summary>Returns the ItemData behind an IItem, or null when it is not an ItemData.</summary>
    public static ItemData AsItemData(this IItem item) => item as ItemData;

    /// <summary>Returns the item's icon, or null when the item is not an ItemData asset.</summary>
    public static UnityEngine.Sprite IconOf(this IItem item) => (item as ItemData)?.icon;
}
