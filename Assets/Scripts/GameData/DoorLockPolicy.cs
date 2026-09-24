namespace Game.Core
{
    /// <summary>
    /// Engine-free gate lock rule: a locked gate opens only when it names a required key and the
    /// interacting holder actually has it. Plain C#, unit-testable without a scene.
    ///
    /// Unity setup: none. Called by SlidingDoor, which resolves the key holder and consumes the key.
    /// </summary>
    public static class DoorLockPolicy
    {
        /// <summary>True when the gate may open (it is unlocked, or the holder has the required key).</summary>
        public static bool CanUnlock(bool isLocked, string requiredKeyId, bool holderHasKey) =>
            !isLocked || (!string.IsNullOrWhiteSpace(requiredKeyId) && holderHasKey);
    }
}
