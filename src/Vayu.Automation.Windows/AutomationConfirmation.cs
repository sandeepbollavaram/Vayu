using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>The user's answer to an automation confirmation prompt.</summary>
public enum AutomationConfirmationDecision
{
    /// <summary>Run this one action now. Not remembered for future actions.</summary>
    ApproveOnce = 0,

    /// <summary>Do not run it. Default for a dismissed or unanswered prompt.</summary>
    Cancel = 1,
}

/// <summary>
/// Everything the user needs to decide whether to allow a single automation
/// action: the target window/app, what Vayu will do, a safe text preview (for
/// typing), the risk level, the reason, and a plain-language safety warning.
/// Carries no secrets and no window contents.
/// </summary>
/// <param name="Target">The visible app/window the action applies to.</param>
/// <param name="ActionType">What Vayu intends to do.</param>
/// <param name="RiskLevel">The assigned risk.</param>
/// <param name="Reason">Short explanation of the action.</param>
/// <param name="SafetyWarning">A plain warning (e.g. "Vayu will type into this window").</param>
/// <param name="TextPreview">For typing: the exact text to be typed. Never a secret (rejected earlier).</param>
/// <param name="CorrelationId">Ties the request to its plan, result, and audit rows.</param>
public sealed record AutomationConfirmationRequest(
    AutomationTarget Target,
    AutomationActionType ActionType,
    RiskLevel RiskLevel,
    string Reason,
    string SafetyWarning,
    Guid CorrelationId,
    string? TextPreview = null)
{
    /// <summary>Builds a confirmation request from an evaluated plan.</summary>
    public static AutomationConfirmationRequest FromPlan(AutomationActionPlan plan, string safetyWarning)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(safetyWarning);
        return new AutomationConfirmationRequest(
            plan.Target,
            plan.ActionType,
            plan.RiskLevel,
            plan.Reason,
            safetyWarning,
            plan.CorrelationId,
            plan.TextPreview);
    }
}

/// <summary>
/// Asks the user to approve a single automation action before it runs. The
/// concrete WinUI dialog arrives in M5.2; until then the default implementation
/// must deny (never silently approve).
/// </summary>
public interface IAutomationConfirmationService
{
    /// <summary>
    /// Shows <paramref name="request"/> and returns the user's decision. A
    /// dismissed or unavailable prompt must resolve to
    /// <see cref="AutomationConfirmationDecision.Cancel"/>.
    /// </summary>
    Task<AutomationConfirmationDecision> RequestAsync(
        AutomationConfirmationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe default <see cref="IAutomationConfirmationService"/>: denies every
/// request. Used until the real confirmation UI ships (M5.2), so no automation
/// can run without an explicit approval path — fail-closed.
/// </summary>
public sealed class DenyingAutomationConfirmationService : IAutomationConfirmationService
{
    /// <inheritdoc />
    public Task<AutomationConfirmationDecision> RequestAsync(
        AutomationConfirmationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AutomationConfirmationDecision.Cancel);
}
