using System;

namespace Game.Core
{
    /// <summary>
    /// Scene-authored starting equipment for an entity, as item ids from items.json. Plain C#,
    /// lives in Game.Data.
    ///
    /// Unity setup: none. Held as a [SerializeField] EquipmentLoadoutConfig field by EquipmentManager.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class EquipmentLoadoutConfig
    {
        public string StartingWeaponItemId = "";
        public string StartingArmorItemId = "";
        public string StartingAccessoryItemId = "";
    }
}
