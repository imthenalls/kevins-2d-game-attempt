using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative tuning and lock configuration for the 3D interactable door (LockedDoor3D).
    /// Plain C#, lives in Game.Data. The Unity-only references (panel Transforms, blocker collider,
    /// LayerMask) stay on the component; colours are stored as RGBA floats.
    ///
    /// Unity setup: none. Held as a [SerializeField] Door3DConfig field by LockedDoor3D.
    /// Runtime API: plain public fields, read by Game.Core.LockedDoorModel.
    /// </summary>
    [Serializable]
    public class Door3DConfig
    {
        public string DisplayName = "Door";
        public float InteractionRange = 1.9f;
        public string RequiredKeyId = "";
        public bool ConsumeKeyOnUnlock = false;
        public bool RemainUnlocked = true;
        public bool StartsOpen = false;
        public bool CanClose = false;
        public float SlideDuration = 0.35f;
        public string LockedMessage = "It's locked. You need the {0}.";
        public float LockedR = 0.95f;
        public float LockedG = 0.28f;
        public float LockedB = 0.16f;
        public float LockedA = 1f;
        public float UnlockedR = 0.22f;
        public float UnlockedG = 0.9f;
        public float UnlockedB = 0.82f;
        public float UnlockedA = 1f;
    }
}
