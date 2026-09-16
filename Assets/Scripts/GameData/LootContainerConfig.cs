using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for LootContainer. Plain C#, lives in Game.Data. The chest id, display name, and the
    /// loot item references stay on the component.
    ///
    /// Unity setup: none. Held as a [SerializeField] LootContainerConfig field by LootContainer.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class LootContainerConfig
    {
        public float InteractionRange = 1.5f;
        public int InventoryRows = 2;
        public int InventoryColumns = 4;
    }
}
