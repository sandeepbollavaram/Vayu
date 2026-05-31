using Vayu.AI.Local;
using Vayu.Core;

namespace Vayu.AgentRuntime;

/// <summary>
/// The M2 AI Router. Implements <see cref="IIntentPlanner"/> so it drops
/// straight into <see cref="AgentRuntime"/> in place of the bare
/// <see cref="IntentPlanner"/>, and chooses between the local AI planner
/// and the deterministic rule-based parser.
/// </summary>
/// <remarks>
/// Routing policy (Offline / Hybrid-later):
/// <list type="number">
/// <item>If offline planning is disabled (the default), use the rule-based parser. No model is contacted.</item>
/// <item>Otherwise ask the local AI planner. If it succeeds and its confidence clears the floor, use that plan.</item>
/// <item>On any local-AI failure — disabled, unavailable, malformed JSON, low confidence, non-allowlisted intent, timeout — fall back to the rule-based parser.</item>
/// </list>
/// The router never throws for a normal AI failure: a hallucinating or
/// offline model degrades to deterministic parsing, so command handling is
/// always available. The resulting plan still flows through
/// <see cref="AgentRuntime"/> → <c>IPermissionService</c> → agent →
/// audit log; the router only decides <i>who wrote the plan</i>, never
/// whether it executes.
///
/// Cloud / Hybrid routing (Gemini fallback) is M3 and intentionally absent here.
/// </remarks>
public sealed class AiRouterIntentPlanner : IIntentPlanner
{
    private readonly RuleBasedCommandParser _ruleParser;
    private readonly ILocalIntentPlanner _localPlanner;
    private readonly LocalAiPlannerOptions _options;
    private readonly LocalAiPlannerState? _state;

    public AiRouterIntentPlanner(
        RuleBasedCommandParser ruleParser,
        ILocalIntentPlanner localPlanner,
        LocalAiPlannerOptions? options = null,
        LocalAiPlannerState? state = null)
    {
        ArgumentNullException.ThrowIfNull(ruleParser);
        ArgumentNullException.ThrowIfNull(localPlanner);
        _ruleParser = ruleParser;
        _localPlanner = localPlanner;
        _options = options ?? new LocalAiPlannerOptions();
        _state = state;
    }

    /// <summary>
    /// Live opt-in switch. Prefers the runtime <see cref="LocalAiPlannerState"/>
    /// (which the Settings toggle flips) and falls back to the static
    /// <see cref="LocalAiPlannerOptions.EnableOfflinePlanning"/> when no state
    /// was supplied (e.g. in unit tests).
    /// </summary>
    private bool OfflineEnabled => _state?.OfflinePlanningEnabled ?? _options.EnableOfflinePlanning;

    /// <inheritdoc />
    public async Task<IntentPlan> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OfflineEnabled)
        {
            return _ruleParser.Parse(request);
        }

        LocalAiPlanningResult result;
        try
        {
            result = await _localPlanner.PlanAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A local-AI failure must never break command handling — fall back.
        catch (Exception)
        {
            return FallBack(request);
        }
#pragma warning restore CA1031

        if (result.Success && result.IntentPlan is { } plan)
        {
            return plan;
        }

        return FallBack(request);
    }

    private IntentPlan FallBack(CommandRequest request)
    {
        var plan = _ruleParser.Parse(request);
        // Mark provenance so logs distinguish "rule-based by config" from
        // "rule-based because the model failed".
        return plan with { PlanSource = "ollama-fallback-rule-based" };
    }
}
