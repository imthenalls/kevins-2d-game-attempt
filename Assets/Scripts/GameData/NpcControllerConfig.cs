using System;

namespace Game.Core
{
    /// <summary>
    /// Numeric tuning/stat values for NpcController (enemy stats, health bar, inventory, trade,
    /// interaction). Plain C#, lives in Game.Data. Identity strings and the NpcType enum stay on
    /// the component; Unity references stay on the component.
    ///
    /// Unity setup: none. Held as a [SerializeField] NpcControllerConfig field by NpcController.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class NpcControllerConfig
    {
        // Enemy stats
        public int EnemyMaxHp = 30;
        public float AggroRange = 3f;

        // Enemy health bar
        public bool ShowEnemyHealthBar = true;
        public float HealthBarWorldOffset = 0.8f;
        public float HealthBarScreenWidth = 72f;
        public float HealthBarScreenHeight = 12f;

        // Inventory
        public int InventoryRows = 3;
        public int InventoryColumns = 4;
        public int TraderStartingMana = 50;
        public int TraderManaCapacity = 500;

        // Interaction
        public float InteractionRange = 1.5f;
    }
}
