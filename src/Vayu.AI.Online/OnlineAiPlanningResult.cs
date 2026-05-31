using Vayu.Core;

namespace Vayu.AI.Online;

/// <summary>
/// Outcome of <see cref="IOnlineAiProvider.PlanAsync"/>. Parallels
/// <c>LocalAiPlanningResult</c> so the AI Router can treat local and cloud
/// planners uniformly and fall back cleanly. The model never executes — it
/// only produces a plan, which the runtime still gates.
/// </summary>
/// <param name="Success">True when <see cref="IntentPlan"/> is populated and passed the safety policy.</param>
/// <param name="IntentPlan">The validated plan. Null when <see cref="Success"/> is false.</param>
/// <param name="ErrorMessage">Already-redacted failure reason. Null on success.</param>
/// <param name="ProviderId">Which provider produced this result, e.g. <c>"gemini"</c>.</param>
/// <param name="ModelName">Which model produced it, e.g. <c>"gemini-2.0-flash"</c>. Null when not applicable.</param>
public sealed record OnlineAiPlanningResult(
    bool Success,
    IntentPlan? IntentPlan,
    string? ErrorMessage,
    string ProviderId,
    string? ModelName)
{
    /// <summary>Builds a success result around a populated, policy-checked plan.</summary>
    public static OnlineAiPlanningResult Succeeded(IntentPlan plan, string providerId, string? modelName = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return new OnlineAiPlanningResult(true, plan, null, providerId, modelName);
    }

    /// <summary>Builds a failure result carrying a redaction-safe reason.</summary>
    public static OnlineAiPlanningResult Failed(string errorMessage, string providerId, string? modelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return new OnlineAiPlanningResult(false, null, errorMessage, providerId, modelName);
    }
}
