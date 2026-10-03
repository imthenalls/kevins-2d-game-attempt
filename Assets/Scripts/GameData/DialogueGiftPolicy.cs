namespace Game.Core
{
    /// <summary>
    /// Engine-free rule for gating an NPC inventory gift behind a world-state flag. A dialogue that
    /// branches (a correct answer versus wrong answers) can set a flag only on the successful branch,
    /// and the NPC only completes its gift when that flag holds. A dialogue with no required flag
    /// behaves exactly as before.
    ///
    /// Unity setup: none. Called by NpcDialogue.GiveInventoryGift.
    /// Runtime API: <see cref="ShouldGive"/>.
    /// </summary>
    public static class DialogueGiftPolicy
    {
        /// <summary>True when the gift may run: no flag is required, or the required flag is set.</summary>
        public static bool ShouldGive(bool requiresFlag, bool flagSet) => !requiresFlag || flagSet;
    }
}
