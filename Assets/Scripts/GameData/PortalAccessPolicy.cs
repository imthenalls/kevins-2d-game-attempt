namespace Game.Core
{
    /// <summary>Why a traveler may or may not use a portal.</summary>
    public enum PortalAccess
    {
        Allowed,
        LockedByWorldState,
        LockedByKey,
    }

    /// <summary>
    /// Engine-free portal access rule: a route requires its world-state unlock flag (if any) to be
    /// satisfied and the traveler's key (if any) to be held. Plain C#, unit-testable without a scene.
    ///
    /// Unity setup: none. Called by PortalManager.
    /// </summary>
    public static class PortalAccessPolicy
    {
        public static PortalAccess Evaluate(bool unlockedByWorldState, bool keySatisfied)
        {
            if (!unlockedByWorldState)
                return PortalAccess.LockedByWorldState;
            if (!keySatisfied)
                return PortalAccess.LockedByKey;

            return PortalAccess.Allowed;
        }
    }
}
