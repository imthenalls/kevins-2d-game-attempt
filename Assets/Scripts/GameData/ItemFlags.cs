namespace Game.Core
{
    /// <summary>
    /// Special behaviours for an item. Persisted by value on ItemData assets.
    ///
    /// Unique    — forces stack size to 1; only one copy can be held.
    /// QuestItem — cannot be dropped or stacked; removed by quest actions.
    /// KeyItem   — cannot be dropped; routed to the player keyring instead of inventory slots.
    /// </summary>
    [System.Flags]
    public enum ItemFlags
    {
        None = 0,
        Unique = 1,
        QuestItem = 2,
        KeyItem = 4,
    }
}
