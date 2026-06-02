using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// Translates a compound "open and type" request (e.g. <c>open notepad and write
/// hello</c>) into safe, separate <see cref="AutomationActionPlan"/>s — an
/// <see cref="AutomationActionType.OpenApp"/> step and a <em>planned</em>
/// <see cref="AutomationActionType.TypeText"/> step. It <b>plans only</b>: the
/// type step is returned with its risk and confirmation requirement so a future
/// executor (M5.2) can run it after the user approves. M5.1 never types.
/// </summary>
/// <remarks>
/// This is the bridge from the rule-based parser's <c>typing_requested</c> flag
/// to the M5 automation model. The open step keeps the existing M1 behaviour;
/// the type step is evaluated by <see cref="AutomationSafetyPolicy"/>, so secret
/// or shell text is rejected up front and any real typing always requires
/// confirmation and the audit log.
/// </remarks>
public sealed class CommandToAutomationPlanner
{
    private readonly AutomationSafetyPolicy _policy;

    public CommandToAutomationPlanner(AutomationSafetyPolicy? policy = null)
    {
        _policy = policy ?? new AutomationSafetyPolicy();
    }

    /// <summary>
    /// Builds the ordered plan for "open <paramref name="appName"/> and type
    /// <paramref name="textToType"/>". The first plan opens the app; the second
    /// is the planned (not executed) type step. When <paramref name="textToType"/>
    /// is empty, only the open step is returned.
    /// </summary>
    public IReadOnlyList<AutomationActionPlan> PlanOpenAndType(
        string appName,
        string? textToType,
        Guid correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        var plans = new List<AutomationActionPlan>
        {
            new(
                AutomationActionType.OpenApp,
                new AutomationTarget(AppName: appName),
                RiskLevel.L1,
                RequiresConfirmation: false,
                $"Open {appName}.",
                correlationId),
        };

        if (!string.IsNullOrWhiteSpace(textToType))
        {
            // Evaluate (not execute) the typing step. The policy rejects secrets
            // and shell text, and flags that confirmation is required for L3+.
            var typePlan = _policy.Evaluate(
                AutomationActionType.TypeText,
                new AutomationTarget(AppName: appName, WindowTitle: appName),
                textToType,
                correlationId);
            plans.Add(typePlan);
        }

        return plans;
    }
}
