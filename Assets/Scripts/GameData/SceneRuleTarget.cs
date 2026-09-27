namespace Game.Core
{
    /// <summary>
    /// Which boolean scene rule to toggle via a trigger. Declared in Core because it is gameplay
    /// vocabulary shared by triggers, zones, and the rule manager.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum SceneRuleTarget
    {
        InventoryLocked,
        SavingEnabled,
        PlayerInvincible,
        PlayerAttackEnabled,
        PlayerMovementEnabled,
        EnemiesInvincible,
        NpcAttackEnabled,
        NpcMovementEnabled,
        NpcDialogueEnabled,
        PortalsBlocked,
        DotEnabled,
        HotEnabled,
    }
}
