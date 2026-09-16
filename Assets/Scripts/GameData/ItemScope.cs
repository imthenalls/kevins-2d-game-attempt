namespace Game.Core
{
    /// <summary>
    /// Which world(s) an item may exist in. Shared items travel between worlds; the others are
    /// confined to their world's inventory.
    /// </summary>
    public enum ItemScope
    {
        Shared,
        WorldA,
        WorldB,
    }
}
