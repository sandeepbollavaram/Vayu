using System.Collections.Immutable;
using System.Text.Json;

using Vayu.Core;

namespace Vayu.AI.Local;

/// <summary>
/// Parses and <b>validates</b> the strict-JSON plan the local model is asked
/// to return, turning it into a safe <see cref="IntentPlan"/>. This is the
/// trust boundary: the model's output is untrusted text, so every field is
/// re-checked here and the risk level is assigned by Vayu — never taken from
/// the model.
/// </summary>
/// <remarks>
/// Allowlisted intents only. The model cannot invent shell commands, file
/// deletes, email sends, typing/clicking, admin actions, or browser-login
/// actions — any intent outside the allowlist is rejected. A request that
/// looks like typing into an app is flagged with the same
/// <c>typing_requested</c> arg the rule-based parser uses, so the
/// AppLauncherAgent defers it to M5 instead of acting on it.
/// </remarks>
public static class OllamaPlanJsonParser
{
    /// <summary>Intents the local model is permitted to emit in M2.7.</summary>
    public const string AppOpenIntent = "app.open";
    public const string ShowLogsIntent = "ui.show_logs";
    public const string ShowSettingsIntent = "ui.show_settings";
    public const string UnknownIntent = "unknown";

    /// <summary>Arg key signalling the user asked to type into an app — deferred to M5.</summary>
    public const string TypingRequestedArgKey = "typing_requested";

    private static readonly ImmutableHashSet<string> AllowedIntents = ImmutableHashSet.Create(
        StringComparer.OrdinalIgnoreCase,
        AppOpenIntent,
        ShowLogsIntent,
        ShowSettingsIntent,
        UnknownIntent);

    /// <summary>
    /// Attempts to parse <paramref name="modelOutput"/> into a validated plan.
    /// </summary>
    /// <param name="modelOutput">The raw text the model returned (may contain surrounding prose).</param>
    /// <param name="correlationId">Correlation id to stamp on the resulting plan.</param>
    /// <param name="planSource">The PlanSource value, e.g. <c>"ollama:gemma3:4b"</c>.</param>
    /// <param name="plan">The validated plan on success.</param>
    /// <param name="error">A redaction-safe error reason on failure.</param>
    /// <returns>True when a safe plan was produced.</returns>
    public static bool TryParse(
        string? modelOutput,
        Guid correlationId,
        string planSource,
        out IntentPlan? plan,
        out string? error)
    {
        plan = null;
        error = null;

        var json = ExtractJsonObject(modelOutput);
        if (json is null)
        {
            error = "Model did not return a JSON object.";
            return false;
        }

        PlanDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PlanDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            error = "Model returned malformed JSON.";
            return false;
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Intent))
        {
            error = "Model plan has no intent.";
            return false;
        }

        var intent = dto.Intent.Trim();
        if (!AllowedIntents.Contains(intent))
        {
            error = $"Model proposed a non-allowlisted intent ('{intent}').";
            return false;
        }
        // Normalise to canonical casing.
        intent = CanonicalIntent(intent);

        if (dto.Confidence is < 0.0 or > 1.0)
        {
            error = "Model confidence is out of range.";
            return false;
        }

        // Vayu assigns risk — never the model. The model's "risk" field is ignored.
        var risk = RiskForIntent(intent);
        var args = ImmutableDictionary<string, string>.Empty;

        if (string.Equals(intent, AppOpenIntent, StringComparison.Ordinal))
        {
            var app = dto.Args?.App?.Trim();
            if (string.IsNullOrWhiteSpace(app))
            {
                error = "app.open requires an 'app' argument.";
                return false;
            }
            // Collapse whitespace to match the rule-based parser ("vs code" -> "vscode").
            app = string.Concat(app.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            args = args.Add("app", app);

            // If the model (or the user's phrasing) asked to type into the app,
            // flag it so the agent defers to M5 — Vayu never types in M2.7.
            if (dto.Args?.TypingRequested == true)
            {
                args = args.Add(TypingRequestedArgKey, "true");
            }
        }

        plan = new IntentPlan
        {
            Intent = intent,
            Risk = risk,
            Args = args,
            Confidence = dto.Confidence,
            PlanSource = planSource,
            CorrelationId = correlationId,
        };
        return true;
    }

    /// <summary>Risk is assigned by Vayu based on the intent, not by the model.</summary>
    public static RiskLevel RiskForIntent(string intent) => intent switch
    {
        AppOpenIntent => RiskLevel.L1,
        ShowLogsIntent => RiskLevel.L0,
        ShowSettingsIntent => RiskLevel.L0,
        _ => RiskLevel.L0,
    };

    private static string CanonicalIntent(string intent)
    {
        if (string.Equals(intent, AppOpenIntent, StringComparison.OrdinalIgnoreCase))
        {
            return AppOpenIntent;
        }
        if (string.Equals(intent, ShowLogsIntent, StringComparison.OrdinalIgnoreCase))
        {
            return ShowLogsIntent;
        }
        if (string.Equals(intent, ShowSettingsIntent, StringComparison.OrdinalIgnoreCase))
        {
            return ShowSettingsIntent;
        }
        return UnknownIntent;
    }

    /// <summary>
    /// Models sometimes wrap JSON in prose or code fences. Pull out the first
    /// balanced <c>{...}</c> block so <see cref="JsonSerializer"/> sees clean input.
    /// </summary>
    private static string? ExtractJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }
        return text.Substring(start, end - start + 1);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record PlanDto
    {
        public string? Intent { get; init; }
        public double? Confidence { get; init; }
        public ArgsDto? Args { get; init; }
    }

    private sealed record ArgsDto
    {
        public string? App { get; init; }
        public bool? TypingRequested { get; init; }
    }
}
