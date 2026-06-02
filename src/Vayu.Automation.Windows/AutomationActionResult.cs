namespace Vayu.Automation.Windows;

/// <summary>Outcome status of a planned/attempted automation action.</summary>
public enum AutomationStatus
{
    /// <summary>The action ran and succeeded.</summary>
    Success = 0,

    /// <summary>The plan is safe but needs the user to approve it before running.</summary>
    NeedsConfirmation = 1,

    /// <summary>The safety policy rejected the action; it will never run as planned.</summary>
    Rejected = 2,

    /// <summary>The user cancelled at confirmation, or execution was stopped.</summary>
    Cancelled = 3,

    /// <summary>The action was attempted and failed.</summary>
    Failed = 4,
}

/// <summary>
/// The result of evaluating or executing an <see cref="AutomationActionPlan"/>.
/// Carries no window contents and no typed text beyond a safe message.
/// </summary>
/// <param name="Success">True only when <see cref="Status"/> is <see cref="AutomationStatus.Success"/>.</param>
/// <param name="Status">The outcome.</param>
/// <param name="Message">Redaction-safe explanation for the UI / audit log.</param>
/// <param name="ActionType">Which action this result describes.</param>
/// <param name="CorrelationId">Ties the result back to its plan and audit rows.</param>
public sealed record AutomationActionResult(
    bool Success,
    AutomationStatus Status,
    string Message,
    AutomationActionType ActionType,
    Guid CorrelationId)
{
    /// <summary>A rejection result (unsafe action).</summary>
    public static AutomationActionResult Rejected(AutomationActionType actionType, Guid correlationId, string message)
        => new(false, AutomationStatus.Rejected, message, actionType, correlationId);

    /// <summary>A needs-confirmation result (safe but unapproved).</summary>
    public static AutomationActionResult NeedsConfirmation(AutomationActionType actionType, Guid correlationId, string message)
        => new(false, AutomationStatus.NeedsConfirmation, message, actionType, correlationId);

    /// <summary>A cancelled result.</summary>
    public static AutomationActionResult Cancelled(AutomationActionType actionType, Guid correlationId, string message)
        => new(false, AutomationStatus.Cancelled, message, actionType, correlationId);
}
