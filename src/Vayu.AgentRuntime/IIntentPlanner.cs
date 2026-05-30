using Vayu.Core;

namespace Vayu.AgentRuntime;

/// <summary>
/// Converts a raw <see cref="CommandRequest"/> into an
/// <see cref="IntentPlan"/>. At Milestone 1 the only implementation is
/// the rule-based parser; M2 swaps in an Ollama-backed planner; M3 adds
/// a Gemini-backed planner with a confidence-based fallback to the
/// rule-based one.
/// </summary>
public interface IIntentPlanner
{
    /// <summary>Plans the user's command and returns the structured intent.</summary>
    Task<IntentPlan> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default);
}
