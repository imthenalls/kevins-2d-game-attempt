/// <summary>
/// Broad category for an item. Used for inventory sorting, UI filtering,
/// and deciding which interactions are valid (e.g. hotbar only accepts Consumables).
///
/// Unity setup: none — assign on each ItemData ScriptableObject in the Inspector.
/// </summary>
public enum ItemType
{
    Misc        = 0,
    Consumable  = 1,
    Material    = 2,
    Equipment   = 3,
}
