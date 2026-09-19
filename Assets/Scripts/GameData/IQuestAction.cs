namespace Game.Core
{
    /// <summary>
    /// A side effect that executes once when a quest node is entered.
    ///
    /// Built-in implementations live in Game.Presentation (they touch the world-state manager,
    /// item database, and quest manager); QuestLoader creates them from the "type" field in JSON.
    ///
    /// Unity setup: none — actions are pure C# objects, not MonoBehaviours.
    /// </summary>
    public interface IQuestAction
    {
        void Execute();
    }
}
