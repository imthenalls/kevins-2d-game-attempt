namespace Game.Core
{
    /// <summary>
    /// Identifies one of the two parallel world layers the player can travel between. Declared in
    /// Core because it is gameplay vocabulary shared by save data, inventory scope, portals, and the
    /// avatar progression model — not a rendering concern.
    ///
    /// Unity setup: none.
    /// </summary>
    public enum WorldLayer
    {
        WorldA,
        WorldB,
    }
}
