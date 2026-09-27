namespace Game.Core
{
    /// <summary>Which physics event fires a rule change.</summary>
    public enum RuleTriggerFireOn
    {
        /// <summary>Fire only when the activator enters the trigger.</summary>
        Enter,

        /// <summary>Fire only when the activator exits the trigger.</summary>
        Exit,

        /// <summary>Fire on enter with <c>value</c>, and on exit with <c>!value</c>.</summary>
        Both,
    }
}
