using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// A single planned automation step — <em>what Vayu intends to do</em>, never an
/// execution. It carries everything the confirmation UI and the audit log need:
/// the action, its target, a redaction-safe text preview (for typing), the risk
/// level, whether confirmation is required, and why.
/// </summary>
/// <param name="ActionType">The kind of action.</param>
/// <param name="Target">The visible app/window the action applies to.</param>
/// <param name="RiskLevel">Vayu-assigned risk (drives the permission engine).</param>
/// <param name="RequiresConfirmation">True when the user must approve before execution.</param>
/// <param name="Reason">Short, human-readable explanation shown to the user.</param>
/// <param name="CorrelationId">Ties the plan to its result and audit rows.</param>
/// <param name="TextPreview">For <see cref="AutomationActionType.TypeText"/>: a safe preview of the text. Never a secret.</param>
public sealed record AutomationActionPlan(
    AutomationActionType ActionType,
    AutomationTarget Target,
    RiskLevel RiskLevel,
    bool RequiresConfirmation,
    string Reason,
    Guid CorrelationId,
    string? TextPreview = null);
