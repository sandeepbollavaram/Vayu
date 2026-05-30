namespace Vayu.AgentRuntime;

/// <summary>
/// Tunable knobs for <see cref="AgentRuntime"/>. Defaults are safe for
/// production; tests can disable audit writes to keep databases clean.
/// </summary>
public sealed record AgentRuntimeOptions
{
    /// <summary>When true, the runtime appends an <c>Actions</c> row for every execution it routes.</summary>
    public bool EnableAuditLog { get; init; } = true;

    /// <summary>Message shown when the planner returns <see cref="RuleBasedCommandParser.UnknownIntent"/>.</summary>
    public string UnknownCommandMessage { get; init; } = "I didn't understand that command. Try 'open notepad' or 'show logs'.";
}
