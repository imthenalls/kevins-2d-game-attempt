namespace Game.Core
{
    /// <summary>
    /// Engine-free state and decision model for a single 3D door: whether it is locked, and the
    /// outcome of an entity trying to use it. The lock rule itself is <see cref="DoorLockPolicy"/>;
    /// this type owns the door's open/unlocked state so the Unity adapter stays a thin facade.
    ///
    /// Unity setup: none (pure C#). Created by LockedDoor3D with its Door3DConfig.
    /// Runtime API: <see cref="ResolveUse"/>, <see cref="ApplyOpen"/>, <see cref="IsOpen"/>,
    /// <see cref="IsLocked"/>, <see cref="IsUnlocked"/>.
    /// </summary>
    public sealed class LockedDoorModel
    {
        private readonly Door3DConfig config;

        public LockedDoorModel(Door3DConfig config)
        {
            this.config = config ?? new Door3DConfig();
            IsOpen = this.config.StartsOpen;
            IsUnlocked = this.config.StartsOpen || string.IsNullOrWhiteSpace(this.config.RequiredKeyId);
        }

        public bool IsOpen { get; private set; }

        /// <summary>True once the door has been unlocked for good (RemainUnlocked) or was never locked.</summary>
        public bool IsUnlocked { get; private set; }

        /// <summary>True while the door still needs its required key.</summary>
        public bool IsLocked => !IsUnlocked && !string.IsNullOrWhiteSpace(config.RequiredKeyId);

        public string RequiredKeyId => config.RequiredKeyId;

        /// <summary>
        /// Resolves a use attempt from an entity carrying (or not) the required key. Reports the
        /// outcome without touching the animation; the adapter applies <see cref="ApplyOpen"/> when
        /// the open animation finishes.
        /// </summary>
        public GateUseResult ResolveUse(bool holderHasKey)
        {
            if (IsOpen)
                return GateUseResult.Opened;

            if (!DoorLockPolicy.CanUnlock(IsLocked, config.RequiredKeyId, holderHasKey))
                return GateUseResult.Locked;

            if (config.RemainUnlocked)
                IsUnlocked = true;

            return GateUseResult.Opened;
        }

        /// <summary>Records the door's physical open state once the adapter's animation settles.</summary>
        public void ApplyOpen(bool open) => IsOpen = open;
    }
}
