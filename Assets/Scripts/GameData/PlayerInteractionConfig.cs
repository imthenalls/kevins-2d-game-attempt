using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for PlayerInteractionController. Plain C#, lives in Game.Data. Layer masks and UI /
    /// controller references stay on the component. Legacy keys are stored as KeyCode ints
    /// (E = 101, Space = 32).
    ///
    /// Unity setup: none. Held as a [SerializeField] PlayerInteractionConfig field by
    /// PlayerInteractionController.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class PlayerInteractionConfig
    {
        public float InteractionSearchRadius = 2f;
        public int LegacyInteractKeyCode = 101; // KeyCode.E
        public int LegacyAdvanceKeyCode = 32;   // KeyCode.Space
        public string LegacyInteractButton = "Submit";
    }
}
