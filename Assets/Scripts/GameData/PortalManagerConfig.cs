using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for PortalManager. Plain C#, lives in Game.Data. The exit velocity is stored as two
    /// floats.
    ///
    /// Unity setup: none. Held as a [SerializeField] PortalManagerConfig field by PortalManager.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class PortalManagerConfig
    {
        public string DefaultTravelerTag = "Player";
        public float TravelerCooldownSeconds = 0.2f;
        public bool ResetVelocityOnTeleport = true;
        public float ExitVelocityX = 0f;
        public float ExitVelocityY = 0f;
    }
}
