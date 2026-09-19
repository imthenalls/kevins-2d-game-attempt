namespace Game.Core
{
    /// <summary>
    /// Condition: the named objective on an active node has reached its required count. Pure and
    /// engine-free, so it lives in Game.Data and is the default condition the Core can build without
    /// the Shell. QuestLoader registers the full factory (including conditions that need world
    /// state or inventories) over this default.
    ///
    /// JSON: { "type": "ObjectiveComplete", "objectiveId": "obj_talk" }
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class ObjectiveCompleteCondition : ICondition
    {
        private readonly string objectiveId;

        public ObjectiveCompleteCondition(string objectiveId) => this.objectiveId = objectiveId;

        public bool Evaluate(QuestInstance ctx) => ctx.IsObjectiveComplete(objectiveId);
    }
}
