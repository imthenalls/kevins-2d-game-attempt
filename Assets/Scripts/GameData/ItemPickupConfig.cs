using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for ItemPickup: how many of the item are given on pickup. Plain C#, lives in
    /// Game.Data. The item reference and player-layer mask stay on the component.
    ///
    /// Unity setup: none. Held as a [SerializeField] ItemPickupConfig field by ItemPickup.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class ItemPickupConfig
    {
        public int Quantity = 1;
    }
}
