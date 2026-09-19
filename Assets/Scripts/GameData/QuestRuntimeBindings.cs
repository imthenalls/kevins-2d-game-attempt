using System;

namespace Game.Core
{
    /// <summary>
    /// Hooks the Shell wires so the engine-free quest runtime can build conditions/actions and log
    /// without referencing UnityEngine. QuestLoader (Game.Presentation) assigns these in a static
    /// constructor; they are invoked through null-safe calls so the Core works without them (for
    /// example in tests that construct a QuestInstance directly).
    ///
    /// Unity setup: none.
    /// </summary>
    public static class QuestRuntimeBindings
    {
        /// <summary>
        /// Builds a condition from JSON data. Defaults to the pure ObjectiveComplete condition so the
        /// Core works standalone (including engine-free tests); QuestLoader overrides it with the
        /// full factory that also handles Fact / QuestInNode / HasItem.
        /// </summary>
        public static Func<QuestConditionData, ICondition> BuildCondition = DefaultBuildCondition;

        /// <summary>Builds an action from JSON data. Set by QuestLoader.</summary>
        public static Func<QuestActionData, IQuestAction> BuildAction;

        /// <summary>Receives diagnostic messages. Set by QuestLoader; silent by default.</summary>
        public static Action<string> Log = _ => { };

        private static ICondition DefaultBuildCondition(QuestConditionData data)
        {
            if (data == null)
                return null;

            return string.Equals(data.type, "ObjectiveComplete", System.StringComparison.OrdinalIgnoreCase)
                ? new ObjectiveCompleteCondition(data.objectiveId)
                : null;
        }
    }
}
