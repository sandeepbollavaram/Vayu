using Vayu.AI.Local;
using Vayu.AI.Online;
using Vayu.Core;

namespace Vayu.AgentRuntime;

/// <summary>
/// The AI Router. Implements <see cref="IIntentPlanner"/> so it drops straight
/// into <see cref="AgentRuntime"/> in place of the bare <see cref="IntentPlanner"/>,
/// and routes each command through one of four modes:
/// <see cref="PlanningMode.RuleBased"/> (default, deterministic),
/// <see cref="PlanningMode.Offline"/> (local model → rule fallback),
/// <see cref="PlanningMode.Online"/> (cloud provider under consent → rule fallback),
/// and <see cref="PlanningMode.Hybrid"/> (local first, cloud-with-consent on low
/// confidence → rule fallback).
/// </summary>
/// <remarks>
/// Invariants (M1 → M3):
/// <list type="bullet">
/// <item>Every AI mode is opt-in. The default is rule-based and contacts no model.</item>
/// <item>A cloud call happens only after <see cref="ICloudConsentService"/> returns <see cref="CloudConsentDecision.AllowOnce"/>. Consent is asked at most once per command.</item>
/// <item>The router never throws for a normal AI/cloud failure — it falls back to the rule-based parser, so command handling is always available.</item>
/// <item>The router only decides <i>who wrote the plan</i>. The resulting plan still flows through <see cref="AgentRuntime"/> → <c>IPermissionService</c> → agent → audit log; nothing here executes a tool.</item>
/// <item>Cloud plans are allowlist/risk-validated inside the provider (<c>OnlineAiSafetyPolicy</c>); the router trusts only <see cref="OnlineAiPlanningResult.Success"/>.</item>
/// </list>
/// </remarks>
public sealed class AiRouterIntentPlanner : IIntentPlanner
{
    private readonly RuleBasedCommandParser _ruleParser;
    private readonly ILocalIntentPlanner _localPlanner;
    private readonly LocalAiPlannerOptions _options;
    private readonly LocalAiPlannerState? _state;
    private readonly IOnlineAiProvider? _onlineProvider;
    private readonly ICloudConsentService? _consentService;

    public AiRouterIntentPlanner(
        RuleBasedCommandParser ruleParser,
        ILocalIntentPlanner localPlanner,
        LocalAiPlannerOptions? options = null,
        LocalAiPlannerState? state = null,
        IOnlineAiProvider? onlineProvider = null,
        ICloudConsentService? consentService = null)
    {
        ArgumentNullException.ThrowIfNull(ruleParser);
        ArgumentNullException.ThrowIfNull(localPlanner);
        _ruleParser = ruleParser;
        _localPlanner = localPlanner;
        _options = options ?? new LocalAiPlannerOptions();
        _state = state;
        _onlineProvider = onlineProvider;
        _consentService = consentService;
    }

    private PlanningMode Mode
    {
        get
        {
            if (_state is not null)
            {
                return _state.Mode;
            }
            // No live state (e.g. unit tests): honour the static offline opt-in.
            return _options.EnableOfflinePlanning ? PlanningMode.Offline : PlanningMode.RuleBased;
        }
    }

    private double ConfidenceFloor => _options.MinimumConfidence;

    /// <inheritdoc />
    public async Task<IntentPlan> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return Mode switch
        {
            PlanningMode.Offline => await PlanOfflineAsync(request, cancellationToken).ConfigureAwait(false),
            PlanningMode.Online => await PlanOnlineAsync(request, cancellationToken).ConfigureAwait(false),
            PlanningMode.Hybrid => await PlanHybridAsync(request, cancellationToken).ConfigureAwait(false),
            _ => _ruleParser.Parse(request),
        };
    }

    // ---- Offline: local model → rule fallback ----

    private async Task<IntentPlan> PlanOfflineAsync(CommandRequest request, CancellationToken ct)
    {
        var local = await TryLocalAsync(request, ct).ConfigureAwait(false);
        return local ?? FallBack(request, "ollama-fallback-rule-based");
    }

    // ---- Online: cloud under consent → rule fallback ----

    private async Task<IntentPlan> PlanOnlineAsync(CommandRequest request, CancellationToken ct)
    {
        if (_onlineProvider is null || _consentService is null)
        {
            return FallBack(request, "online-unavailable-rule-based");
        }

        var decision = await AskConsentAsync(request, ct).ConfigureAwait(false);
        switch (decision)
        {
            case CloudConsentDecision.AllowOnce:
                var cloud = await TryCloudAsync(request, decision, ct).ConfigureAwait(false);
                return cloud ?? FallBack(request, "gemini-fallback-rule-based");

            case CloudConsentDecision.UseLocalInstead:
                // Honour the user's preference: local if available, else rule-based.
                var local = await TryLocalAsync(request, ct).ConfigureAwait(false);
                return local ?? FallBack(request, "cloud-use-local-fallback");

            default: // Cancel / dismiss
                return FallBack(request, "cloud-consent-cancelled-rule-based");
        }
    }

    // ---- Hybrid: local first; consent-gated cloud on low confidence ----

    private async Task<IntentPlan> PlanHybridAsync(CommandRequest request, CancellationToken ct)
    {
        // 1) Try the local model. A confident, allowlisted local plan wins with no cloud call.
        var local = await TryLocalAsync(request, ct).ConfigureAwait(false);
        if (local is not null)
        {
            return local;
        }

        // 2) Local failed or was low-confidence. Offer the cloud — once.
        if (_onlineProvider is null || _consentService is null)
        {
            return FallBack(request, "hybrid-local-failed-rule-based");
        }

        var decision = await AskConsentAsync(request, ct).ConfigureAwait(false);
        switch (decision)
        {
            case CloudConsentDecision.AllowOnce:
                var cloud = await TryCloudAsync(request, decision, ct).ConfigureAwait(false);
                return cloud ?? FallBack(request, "hybrid-gemini-fallback-rule-based");

            default: // Cancel / dismiss / UseLocalInstead all mean "no cloud" here.
                return FallBack(request, "hybrid-consent-declined-rule-based");
        }
    }

    // ---- shared helpers ----

    /// <summary>Runs the local planner; returns a plan only when it succeeds above the confidence floor. Never throws.</summary>
    private async Task<IntentPlan?> TryLocalAsync(CommandRequest request, CancellationToken ct)
    {
        LocalAiPlanningResult result;
        try
        {
            result = await _localPlanner.PlanAsync(request, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A local-AI failure must never break command handling.
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031

        if (result.Success && result.IntentPlan is { } plan && ClearsFloor(plan))
        {
            return plan;
        }
        return null;
    }

    /// <summary>Runs the cloud provider; returns a plan only when it succeeds above the floor. Never throws.</summary>
    private async Task<IntentPlan?> TryCloudAsync(CommandRequest request, CloudConsentDecision decision, CancellationToken ct)
    {
        OnlineAiPlanningResult result;
        try
        {
            result = await _onlineProvider!.PlanAsync(request, decision, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A cloud failure must never break command handling.
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031

        if (result.Success && result.IntentPlan is { } plan && ClearsFloor(plan))
        {
            return plan;
        }
        return null;
    }

    private async Task<CloudConsentDecision> AskConsentAsync(CommandRequest request, CancellationToken ct)
    {
        var providerId = _state?.ActiveOnlineProviderId ?? _onlineProvider!.ProviderId;
        var prompt = (request.Text ?? string.Empty).Trim();
        var consentRequest = new CloudConsentRequest(
            ProviderId: providerId,
            ProviderDisplayName: providerId,
            Purpose: "Plan your command",
            DataSummary: "your typed command text",
            EstimatedPromptChars: prompt.Length,
            RequiresSensitiveContext: false,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        try
        {
            return await _consentService!.RequestConsentAsync(consentRequest, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A consent-dialog failure must fail safe to Cancel.
        catch (Exception)
        {
            return CloudConsentDecision.Cancel;
        }
#pragma warning restore CA1031
    }

    private bool ClearsFloor(IntentPlan plan)
        => plan.Confidence is not { } c || c >= ConfidenceFloor;

    private IntentPlan FallBack(CommandRequest request, string planSource)
    {
        var plan = _ruleParser.Parse(request);
        // Mark provenance so logs distinguish the reason for the fallback.
        return plan with { PlanSource = planSource };
    }
}
