namespace Game.Core
{
    /// <summary>
    /// How serious a data-validation finding is. Errors mean the game will misbehave at runtime;
    /// warnings are suspicious but survivable.
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// One finding from a data-validation pass. Plain value type with no Unity dependency, so the
    /// checks that produce it can be unit tested.
    ///
    /// Unity setup: none.
    /// </summary>
    public readonly struct ValidationIssue
    {
        public readonly ValidationSeverity Severity;
        public readonly string Code;
        public readonly string Message;

        public ValidationIssue(ValidationSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString() => $"[{Severity}] {Code}: {Message}";
    }
}
