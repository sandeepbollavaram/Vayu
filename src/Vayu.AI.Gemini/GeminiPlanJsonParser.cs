using System.Collections.Immutable;
using System.Text.Json;

using Vayu.AI.Online;
using Vayu.Core;

namespace Vayu.AI.Gemini;

/// <summary>
/// Parses and validates the strict-JSON plan Gemini is asked to return. This
/// is the trust boundary for cloud output: every field is re-checked, the
/// intent must be allowlisted, and the risk is assigned by Vayu via
/// <see cref="OnlineAiSafetyPolicy"/> — never taken from the model.
/// </summary>
internal static class GeminiPlanJsonParser
{
    public const string TypingRequestedArgKey = "typing_requested";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Attempts to turn raw model text into a validated <see cref="IntentPlan"/>.</summary>
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
        if (!OnlineAiSafetyPolicy.IsIntentAllowed(intent))
        {
            error = $"Model proposed a non-allowlisted intent ('{intent}').";
            return false;
        }
        intent = CanonicalIntent(intent);

        if (dto.Confidence is < 0.0 or > 1.0)
        {
            error = "Model confidence is out of range.";
            return false;
        }

        // Risk is assigned by Vayu from the intent — the model's "risk" field is ignored.
        var risk = OnlineAiSafetyPolicy.RiskForIntent(intent);
        var args = ImmutableDictionary<string, string>.Empty;

        if (string.Equals(intent, OnlineAiSafetyPolicy.AppOpenIntent, StringComparison.Ordinal))
        {
            var app = dto.Args?.App?.Trim();
            if (string.IsNullOrWhiteSpace(app))
            {
                error = "app.open requires an 'app' argument.";
                return false;
            }
            app = string.Concat(app.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            args = args.Add("app", app);

            if (dto.Args?.TypingRequested == true)
            {
                // Flag typing so the agent defers to M5 — Vayu never types in M3.
                args = args.Add(TypingRequestedArgKey, "true");
            }
        }

        var built = new IntentPlan
        {
            Intent = intent,
            Risk = risk,
            Args = args,
            Confidence = dto.Confidence,
            PlanSource = planSource,
            CorrelationId = correlationId,
        };

        // Defence in depth: run the built plan back through the shared policy.
        if (!OnlineAiSafetyPolicy.Validate(built, out var policyError))
        {
            error = policyError;
            return false;
        }

        plan = built;
        return true;
    }

    private static string CanonicalIntent(string intent)
    {
        if (string.Equals(intent, OnlineAiSafetyPolicy.AppOpenIntent, StringComparison.OrdinalIgnoreCase))
        {
            return OnlineAiSafetyPolicy.AppOpenIntent;
        }
        if (string.Equals(intent, OnlineAiSafetyPolicy.ShowLogsIntent, StringComparison.OrdinalIgnoreCase))
        {
            return OnlineAiSafetyPolicy.ShowLogsIntent;
        }
        if (string.Equals(intent, OnlineAiSafetyPolicy.ShowSettingsIntent, StringComparison.OrdinalIgnoreCase))
        {
            return OnlineAiSafetyPolicy.ShowSettingsIntent;
        }
        return OnlineAiSafetyPolicy.UnknownIntent;
    }

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
