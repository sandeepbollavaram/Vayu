using System.Collections.Immutable;

using Vayu.Core;

namespace Vayu.AI.Online;

/// <summary>
/// The trust boundary for cloud-produced plans. A remote model's output is
/// untrusted, so before any <see cref="IntentPlan"/> from a provider is
/// returned it must pass this policy: an allowlisted intent and a risk no
/// higher than M3 permits. Risk is assigned by Vayu from the intent — the
/// model's claimed risk is ignored.
/// </summary>
/// <remarks>
/// In M3 the online planner is held to the same narrow vocabulary as the
/// offline planner — open apps and navigate Vayu's own UI. It cannot emit
/// shell, file-delete, email, click, type, or admin intents; those carry
/// higher risk and belong to later milestones with their own gating.
/// </remarks>
public static class OnlineAiSafetyPolicy
{
    public const string AppOpenIntent = "app.open";
    public const string ShowLogsIntent = "ui.show_logs";
    public const string ShowSettingsIntent = "ui.show_settings";
    public const string UnknownIntent = "unknown";

    /// <summary>The highest risk a cloud planner may produce in M3.</summary>
    public const RiskLevel MaxAllowedRisk = RiskLevel.L1;

    private static readonly ImmutableHashSet<string> AllowedIntents = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        AppOpenIntent,
        ShowLogsIntent,
        ShowSettingsIntent,
        UnknownIntent);

    /// <summary>True when <paramref name="intent"/> is in the M3 cloud allowlist.</summary>
    public static bool IsIntentAllowed(string? intent)
        => !string.IsNullOrWhiteSpace(intent) && AllowedIntents.Contains(intent.Trim());

    /// <summary>The risk Vayu assigns to an allowlisted intent (never taken from the model).</summary>
    public static RiskLevel RiskForIntent(string intent) => intent switch
    {
        AppOpenIntent => RiskLevel.L1,
        ShowLogsIntent => RiskLevel.L0,
        ShowSettingsIntent => RiskLevel.L0,
        _ => RiskLevel.L0,
    };

    /// <summary>
    /// Validates a cloud-produced plan. Rejects non-allowlisted intents and any
    /// plan whose risk exceeds <see cref="MaxAllowedRisk"/>.
    /// </summary>
    /// <param name="plan">The plan to check.</param>
    /// <param name="error">A redaction-safe rejection reason on failure.</param>
    /// <returns>True when the plan is safe to return to the router.</returns>
    public static bool Validate(IntentPlan? plan, out string? error)
    {
        error = null;
        if (plan is null)
        {
            error = "No plan produced.";
            return false;
        }
        if (!IsIntentAllowed(plan.Intent))
        {
            error = $"Online planner proposed a non-allowlisted intent ('{plan.Intent}').";
            return false;
        }
        if (plan.Risk > MaxAllowedRisk)
        {
            error = $"Online planner proposed risk {plan.Risk}, above the M3 ceiling {MaxAllowedRisk}.";
            return false;
        }
        return true;
    }
}
