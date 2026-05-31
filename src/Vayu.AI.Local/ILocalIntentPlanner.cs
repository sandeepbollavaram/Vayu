using Vayu.Core;

namespace Vayu.AI.Local;

/// <summary>
/// Local-AI-backed <c>IIntentPlanner</c>. M2.7 ships the first concrete
/// implementation (Ollama JSON-mode), wired through the M2-era AI Router
/// alongside the existing <c>RuleBasedCommandParser</c>.
/// </summary>
/// <remarks>
/// Returns a <see cref="LocalAiPlanningResult"/> rather than a raw
/// <see cref="IntentPlan"/> so callers can distinguish "model produced
/// a plan" from "model failed; fall back to rule-based" without losing
/// the failure reason for logs.
/// </remarks>
public interface ILocalIntentPlanner
{
    /// <summary>Asks the local AI to plan the user's command.</summary>
    Task<LocalAiPlanningResult> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default);
}
