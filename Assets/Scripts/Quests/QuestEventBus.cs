using System;

/// <summary>
/// Decoupled event bus for quest-relevant game events.
/// Game systems (combat, inventory, dialogue, portals) call Raise() with no
/// knowledge of the quest system. QuestManager subscribes to OnEvent and
/// routes events to active quest instances.
///
/// Usage:
///   QuestEventBus.Raise("EnemyKilled", enemy.id);
///   QuestEventBus.Raise("ItemCollected", item.itemName);
///   QuestEventBus.Raise("NpcTalkedTo", npc.NpcId);
///   QuestEventBus.Raise("LocationReached", areaId);
/// </summary>
public static class QuestEventBus
{
    /// <summary>
    /// Fired when a game event occurs. Parameters: (eventType, targetId, amount).
    /// </summary>
    public static event Action<string, string, int> OnEvent;

    /// <summary>
    /// Fire a quest-relevant game event.
    /// </summary>
    /// <param name="eventType">Category of event, e.g. "EnemyKilled", "NpcTalkedTo".</param>
    /// <param name="targetId">Specific identifier of the subject, e.g. enemy id or NPC id.</param>
    /// <param name="amount">How many occurred. Defaults to 1.</param>
    public static void Raise(string eventType, string targetId, int amount = 1)
    {
        OnEvent?.Invoke(eventType, targetId, amount);
    }
}
