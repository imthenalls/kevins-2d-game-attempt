using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for WorldObject. Plain C#, lives in Game.Data. The display name, dialogue lines, and
    /// reward item reference stay on the component.
    ///
    /// Unity setup: none. Held as a [SerializeField] WorldObjectConfig field by WorldObject.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class WorldObjectConfig
    {
        public float InteractionRange = 1.5f;
        public int RewardQuantity = 1;
        public bool OneTimeOnly = true;
    }
}
