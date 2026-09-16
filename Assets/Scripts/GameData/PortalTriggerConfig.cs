using System;

namespace Game.Core
{
    /// <summary>
    /// Transfer tuning for PortalTrigger2D. Plain C#, lives in Game.Data. Routing fields
    /// (scene/portal ids, world layer) and the exit Transform stay on the component.
    ///
    /// Unity setup: none. Held as a [SerializeField] PortalTriggerConfig field by PortalTrigger2D.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class PortalTriggerConfig
    {
        public float TravelCooldown = 0.2f;
        public string RequiredTag = "Player";
    }
}
