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
/// <item><c>open &lt;app&gt; and/then write/type &lt;text&gt;</c> → <c>app.open</c> with <see cref="TypingRequestedArgKey"/>=<c>true</c>. The agent then returns NeedsClarification because typing into apps is M5 scope.</item>
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
///
/// TODO (M5): typing/clicking inside apps belongs to M5 Advanced Desktop
/// Automation and must require explicit user confirmation per the
/// permission model. The parser here only flags the intent; the agent
/// refuses to act on it until M5 ships.
/// </remarks>
public sealed class RuleBasedCommandParser
{
    /// <summary>The intent emitted for app-launch commands.</summary>
    public const string AppOpenIntent = "app.open";

    /// <summary>The intent emitted for "open &lt;app&gt; and write/type &lt;text&gt;" — handled by the desktop automation agent (M5.2).</summary>
    public const string OpenAndTypeIntent = "desktop.open_and_type";

    /// <summary>Args key carrying the text to type for <see cref="OpenAndTypeIntent"/>.</summary>
    public const string TextArgKey = "text";

    /// <summary>The intent emitted for "show logs".</summary>
    public const string ShowLogsIntent = "ui.show_logs";

    /// <summary>The intent emitted for "show settings".</summary>
    public const string ShowSettingsIntent = "ui.show_settings";

    /// <summary>The intent emitted when nothing matches.</summary>
    public const string UnknownIntent = "unknown";

    /// <summary>
    /// Args key the parser sets to <c>"true"</c> when the user asked to
    /// type/write something into the app ("open notepad and write hello").
    /// The AppLauncherAgent rejects the plan with a NeedsClarification
    /// pointing to M5 — Vayu never silently types in M1.
    /// </summary>
    public const string TypingRequestedArgKey = "typing_requested";

    private const string PlanSource = "rule-based";

    private static readonly string[] TypingConnectors = { "and", "then" };
    private static readonly string[] TypingVerbs = { "write", "type" };

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
        if (TryParseOpen(normalized, out var app, out var typingRequested, out var textTokenStart))
        {
            if (typingRequested)
            {
                // "open <app> and write <text>" → desktop.open_and_type (L3). The
                // text is taken from the ORIGINAL request to preserve its casing;
                // the desktop automation agent confirms + types only on approval.
                var text = ExtractTypedText(request.Text, textTokenStart);
                var typeArgs = ImmutableDictionary<string, string>.Empty
                    .Add("app", app)
                    .Add(TextArgKey, text);
                return BuildPlan(OpenAndTypeIntent, RiskLevel.L3, request.CorrelationId, args: typeArgs);
            }

            var args = ImmutableDictionary<string, string>.Empty.Add("app", app);
            return BuildPlan(
                AppOpenIntent,
                RiskLevel.L1,
                request.CorrelationId,
                args: args);
        }

        return BuildPlan(UnknownIntent, RiskLevel.L0, request.CorrelationId);
    }

    private static bool TryParseOpen(string normalized, out string app, out bool typingRequested, out int textWordIndex)
    {
        app = string.Empty;
        typingRequested = false;
        textWordIndex = -1;

        const string prefix = "open ";
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var tokens = normalized[prefix.Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        // Look for the typing clause boundary: a "and|then" + "write|type"
        // pair somewhere in the token stream. If found, the app name is
        // whatever came before; if found at index 0, there's no app name.
        var boundary = -1;
        for (var i = 0; i < tokens.Length - 1; i++)
        {
            if (IsTypingBoundary(tokens[i], tokens[i + 1]))
            {
                boundary = i;
                break;
            }
        }

        IEnumerable<string> appTokens;
        if (boundary == 0)
        {
            // "open and write hello" — no app named.
            return false;
        }
        if (boundary > 0)
        {
            typingRequested = true;
            appTokens = tokens.Take(boundary);
            // Text words start after the connector + verb (2 tokens), in the
            // "open " prefix-stripped word stream: 1 (open) + boundary + 2.
            textWordIndex = 1 + boundary + 2;
        }
        else
        {
            appTokens = tokens;
        }

        // Collapse internal whitespace so "vs code" → "vscode".
        app = string.Concat(appTokens);
        return app.Length > 0;
    }

    /// <summary>
    /// Extracts the original-cased text to type, starting at
    /// <paramref name="wordIndex"/> in the whole request's word stream.
    /// </summary>
    private static string ExtractTypedText(string? rawText, int wordIndex)
    {
        if (string.IsNullOrWhiteSpace(rawText) || wordIndex < 0)
        {
            return string.Empty;
        }
        var words = rawText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return wordIndex >= words.Length
            ? string.Empty
            : string.Join(' ', words.Skip(wordIndex));
    }

    private static bool IsTypingBoundary(string left, string right)
    {
        return Array.Exists(TypingConnectors, c => string.Equals(c, left, StringComparison.Ordinal))
            && Array.Exists(TypingVerbs,      v => string.Equals(v, right, StringComparison.Ordinal));
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
