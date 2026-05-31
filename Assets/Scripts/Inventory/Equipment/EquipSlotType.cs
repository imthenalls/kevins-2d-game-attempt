/// <summary>
/// The three equipment slot categories an entity can fill.
/// Assign the matching value on each ItemData asset so EquipmentManager
/// can reject items placed into the wrong slot type.
///
/// Unity setup: none — used as a field on ItemData and EquipmentModel.
/// </summary>
public enum EquipSlotType
{
    Weapon    = 0,
    Armor     = 1,
    Accessory = 2
}
