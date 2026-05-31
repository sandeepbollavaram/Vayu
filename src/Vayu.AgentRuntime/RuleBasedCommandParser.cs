using System.Collections.Immutable;

using Vayu.Core;

namespace Vayu.AgentRuntime;

/// <summary>
/// Deterministic, AI-free parser for Vayu's Milestone-1 command vocabulary.
/// Produces an <see cref="IntentPlan"/> with intent name, args, and the
/// declared risk level for the action.
/// </summary>
/// <remarks>
/// Vocabulary:
/// <list type="bullet">
/// <item><c>open &lt;any app name&gt;</c> → <c>app.open</c> (L1). The catalog/agent decides whether the app actually exists.</item>
/// <item><c>show logs</c> → <c>ui.show_logs</c> (L0)</item>
/// <item><c>show settings</c> → <c>ui.show_settings</c> (L0)</item>
/// <item>anything else → <c>unknown</c> (L0) — the runtime turns this into <c>NeedsClarification</c>.</item>
/// </list>
/// The parser is case-insensitive, trims whitespace, and collapses
/// internal spaces (so <c>"open vs code"</c> reaches the agent as
/// <c>"vscode"</c>). It does NOT whitelist app names: <c>"open spotify"</c>
/// becomes <c>app.open</c> with <c>app=spotify</c>, and the
/// <c>AppLauncherAgent</c> resolves it against <c>KnownAppCatalog</c> and
/// the installed-app catalog.
/// </remarks>
public sealed class RuleBasedCommandParser
{
    /// <summary>The intent emitted for app-launch commands.</summary>
    public const string AppOpenIntent = "app.open";

    /// <summary>The intent emitted for "show logs".</summary>
    public const string ShowLogsIntent = "ui.show_logs";

    /// <summary>The intent emitted for "show settings".</summary>
    public const string ShowSettingsIntent = "ui.show_settings";

    /// <summary>The intent emitted when nothing matches.</summary>
    public const string UnknownIntent = "unknown";

    private const string PlanSource = "rule-based";

    /// <summary>Parses <paramref name="request"/> into an <see cref="IntentPlan"/>.</summary>
    public IntentPlan Parse(CommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalized = (request.Text ?? string.Empty).Trim().ToLowerInvariant();

        if (string.Equals(normalized, "show logs", StringComparison.Ordinal))
        {
            return BuildPlan(ShowLogsIntent, RiskLevel.L0, request.CorrelationId);
        }
        if (string.Equals(normalized, "show settings", StringComparison.Ordinal))
        {
            return BuildPlan(ShowSettingsIntent, RiskLevel.L0, request.CorrelationId);
        }
        if (TryParseOpen(normalized, out var app))
        {
            return BuildPlan(
                AppOpenIntent,
                RiskLevel.L1,
                request.CorrelationId,
                args: ImmutableDictionary<string, string>.Empty.Add("app", app));
        }

        return BuildPlan(UnknownIntent, RiskLevel.L0, request.CorrelationId);
    }

    private static bool TryParseOpen(string normalized, out string app)
    {
        app = string.Empty;
        const string prefix = "open ";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        var rest = normalized[prefix.Length..].Trim();
        if (rest.Length == 0)
        {
            return false;
        }

        // Collapse internal whitespace so "vs code" → "vscode".
        app = string.Concat(rest.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return app.Length > 0;
    }

    private static IntentPlan BuildPlan(
        string intent,
        RiskLevel risk,
        Guid correlationId,
        IReadOnlyDictionary<string, string>? args = null)
        => new()
        {
            Intent = intent,
            Risk = risk,
            Args = args ?? ImmutableDictionary<string, string>.Empty,
            Confidence = 1.0,
            PlanSource = PlanSource,
            CorrelationId = correlationId,
        };
}
