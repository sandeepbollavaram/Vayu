using Vayu.Core;

namespace Vayu.AI.Local;

/// <summary>
/// Outcome of <see cref="ILocalIntentPlanner.PlanAsync"/>. Designed to
/// be diffable in logs: when the model fails, the AI Router falls back
/// to <c>RuleBasedCommandParser</c> and we still want the failure reason
/// (redaction-safe) for the audit log.
/// </summary>
/// <param name="Success">True when <see cref="IntentPlan"/> is populated and safe to dispatch.</param>
/// <param name="IntentPlan">The plan produced by the model. Null when <see cref="Success"/> is false.</param>
/// <param name="ErrorMessage">Already-redacted reason for failure. Null on success.</param>
/// <param name="ProviderName">Which provider produced this result, e.g. <c>"ollama"</c>.</param>
/// <param name="ModelTag">Which model tag produced this result, e.g. <c>"gemma3:4b"</c>.</param>
public sealed record LocalAiPlanningResult(
    bool Success,
    IntentPlan? IntentPlan,
    string? ErrorMessage,
    string ProviderName,
    string ModelTag)
{
    /// <summary>Convenience: builds a success result around a populated plan.</summary>
    public static LocalAiPlanningResult Succeeded(IntentPlan plan, string providerName, string modelTag)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelTag);
        return new LocalAiPlanningResult(true, plan, null, providerName, modelTag);
    }

    /// <summary>Convenience: builds a failure result carrying a redaction-safe reason.</summary>
    public static LocalAiPlanningResult Failed(string errorMessage, string providerName, string modelTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelTag);
        return new LocalAiPlanningResult(false, null, errorMessage, providerName, modelTag);
    }
}
