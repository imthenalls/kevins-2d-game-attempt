using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative tuning and lock configuration for SlidingDoor. Plain C#, lives in Game.Data.
    /// The Unity-only references (Grid, gate Sprite, traveler LayerMask) stay on the component.
    /// Lock colors are stored as RGBA floats and the axis as the DoorAxis enum.
    ///
    /// Unity setup: none. Held as a [SerializeField] SlidingDoorConfig field by SlidingDoor.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class SlidingDoorConfig
    {
        public DoorAxis Axis = DoorAxis.GridX;
        public int CellLength = 2;
        public bool ReverseSlideDirection = false;
        public float SlideDuration = 0.45f;
        public bool CloseAfterPassing = true;
        public float CloseDelay = 0.25f;
        public string DisplayName = "Gate";
        public float InteractionRange = 1f;
        public bool StartsOpen = false;
        public bool CanClose = true;
        public string RequiredKeyId = "golden_key";
        public bool ConsumeKeyOnUnlock = false;
        public bool RemainUnlocked = true;
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
