namespace Game.Core
{
    /// <summary>
    /// Evaluates to true or false given the current quest instance context. All conditions on a
    /// transition must evaluate to true for the transition to fire. To express OR logic, use
    /// multiple transitions pointing at the same target node.
    ///
    /// Built-in implementations live in Game.Presentation (they read world state and inventories);
    /// QuestLoader creates them from the "type" field in JSON.
    ///
    /// Unity setup: none — conditions are pure C# objects, not MonoBehaviours.
    /// </summary>
    public interface ICondition
    {
        bool Evaluate(QuestInstance ctx);
    }
}
