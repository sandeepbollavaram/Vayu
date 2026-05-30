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
/// <item><c>open chrome | edge | vscode | vs code | notepad | terminal | downloads</c> → <c>app.open</c> (L1)</item>
/// <item><c>show logs</c> → <c>ui.show_logs</c> (L0)</item>
/// <item><c>show settings</c> → <c>ui.show_settings</c> (L0)</item>
/// <item>anything else → <c>unknown</c> (L0) — the runtime turns this into <c>NeedsClarification</c>.</item>
/// </list>
/// The parser is case-insensitive, trims whitespace, and normalises
/// <c>"vs code"</c> to <c>"vscode"</c>. No I/O, no async; tests can drive
/// it deterministically.
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

    private static readonly ImmutableDictionary<string, string> KnownApps =
        ImmutableDictionary.CreateRange(StringComparer.Ordinal, new[]
        {
            KeyValuePair.Create("chrome",    "chrome"),
            KeyValuePair.Create("edge",      "edge"),
            KeyValuePair.Create("vscode",    "vscode"),
            KeyValuePair.Create("notepad",   "notepad"),
            KeyValuePair.Create("terminal",  "terminal"),
            KeyValuePair.Create("downloads", "downloads"),
        });

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

        // "vs code" → "vscode", "vs    code" → "vscode"
        var collapsed = string.Concat(rest.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (KnownApps.TryGetValue(collapsed, out var canonical))
        {
            app = canonical;
            return true;
        }
        return false;
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
