namespace Vayu.Core;

/// <summary>
/// Coarse outcome of a single <see cref="CommandRequest"/> after planning,
/// permission, and (when allowed) execution.
/// </summary>
public enum CommandStatus
{
    /// <summary>The command ran and completed successfully.</summary>
    Success = 0,

    /// <summary>The command was attempted but failed. See <see cref="CommandResult.Message"/>.</summary>
    Failed = 1,

    /// <summary>The command needs the user to confirm before it can run.</summary>
    PermissionRequired = 2,

    /// <summary>The user (or policy) cancelled the command.</summary>
    Cancelled = 3,

    /// <summary>The runtime needs more information from the user to plan the command.</summary>
    NeedsClarification = 4,
}

/// <summary>
/// Unified result type returned by every layer of the runtime. The UI
/// renders any agent's outcome through this single shape.
/// </summary>
/// <remarks>
/// Prefer the static factory methods over the object initializer — they
/// document intent and make sure required fields for each status are set.
/// </remarks>
public sealed record CommandResult
{
    /// <summary>The status of the outcome.</summary>
    public required CommandStatus Status { get; init; }

    /// <summary>Human-readable message for UI / logs. Already redacted upstream.</summary>
    public string? Message { get; init; }

    /// <summary>Name of the agent that produced this result, if any.</summary>
    public string? AgentName { get; init; }

    /// <summary>The plan this result was produced from, if available.</summary>
    public IntentPlan? Plan { get; init; }

    /// <summary>Stable short code for programmatic failure handling, e.g. <c>"OLLAMA_UNREACHABLE"</c>.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>When <see cref="Status"/> is <see cref="CommandStatus.NeedsClarification"/>, the question to ask the user.</summary>
    public string? ClarificationPrompt { get; init; }

    /// <summary>Builds a successful result.</summary>
    public static CommandResult Success(
        string? message = null,
        string? agentName = null,
        IntentPlan? plan = null)
        => new()
        {
            Status = CommandStatus.Success,
            Message = message,
            AgentName = agentName,
            Plan = plan,
        };

    /// <summary>Builds a failed result. <paramref name="message"/> should already be redacted.</summary>
    public static CommandResult Failed(
        string message,
        string? errorCode = null,
        string? agentName = null,
        IntentPlan? plan = null)
        => new()
        {
            Status = CommandStatus.Failed,
            Message = message,
            ErrorCode = errorCode,
            AgentName = agentName,
            Plan = plan,
        };

    /// <summary>Builds a "needs the user to confirm" result. The plan is required so the UI can render it.</summary>
    public static CommandResult PermissionRequired(IntentPlan plan, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new()
        {
            Status = CommandStatus.PermissionRequired,
            Plan = plan,
            Message = message ?? "Confirmation required.",
        };
    }

    /// <summary>Builds a cancelled result (policy denial or user cancel).</summary>
    public static CommandResult Cancelled(string? message = null, IntentPlan? plan = null)
        => new()
        {
            Status = CommandStatus.Cancelled,
            Plan = plan,
            Message = message ?? "Cancelled.",
        };

    /// <summary>Builds a "ask the user a follow-up" result.</summary>
    public static CommandResult NeedsClarification(string clarificationPrompt, IntentPlan? plan = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clarificationPrompt);
        return new()
        {
            Status = CommandStatus.NeedsClarification,
            Plan = plan,
            ClarificationPrompt = clarificationPrompt,
        };
    }
}
