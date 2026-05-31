using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Vayu.Core;

namespace Vayu.AI.Local;

/// <summary>
/// First concrete <see cref="ILocalIntentPlanner"/> — asks a local Ollama
/// model to turn a command into a strict-JSON plan, then validates that
/// plan with <see cref="OllamaPlanJsonParser"/>.
/// </summary>
/// <remarks>
/// Safety posture:
/// <list type="bullet">
/// <item>The model never executes anything. It returns JSON; the AI Router hands the validated plan to <c>AgentRuntime</c>, which still gates it through <c>IPermissionService</c>.</item>
/// <item>Risk is assigned by Vayu, not the model.</item>
/// <item>Only allowlisted intents survive validation (app.open / ui.show_logs / ui.show_settings / unknown).</item>
/// <item>Malformed JSON, HTTP errors, timeouts, and out-of-range confidence all yield a <see cref="LocalAiPlanningResult"/> failure — the router then falls back to the rule-based parser. This method never throws for a normal model failure.</item>
/// <item>No cloud calls. No telemetry. The only network egress is the local Ollama endpoint.</item>
/// </list>
/// </remarks>
public sealed class OllamaIntentPlanner : ILocalIntentPlanner
{
    /// <summary>Ollama text-generation endpoint path.</summary>
    public const string GeneratePath = "api/generate";

    /// <summary>Stable provider id used in <see cref="IntentPlan.PlanSource"/> (e.g. <c>ollama:gemma3:4b</c>).</summary>
    public const string ProviderName = "ollama";

    private const string SystemPrompt =
        "You convert a user's desktop command into a single JSON object and nothing else. " +
        "Allowed intents: \"app.open\" (open an installed app), \"ui.show_logs\", \"ui.show_settings\", \"unknown\". " +
        "Schema: {\"intent\":string,\"confidence\":number 0..1,\"args\":{\"app\":string optional,\"typingRequested\":boolean optional},\"reason\":string}. " +
        "Rules: For \"open X\" use intent \"app.open\" with args.app set to X. " +
        "If the user asks to type, write, or enter text into an app, set args.typingRequested to true. " +
        "Never invent shell commands, file deletions, emails, clicks, logins, or admin actions; use \"unknown\" if unsure. " +
        "Reply with only the JSON object.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OllamaProviderOptions _providerOptions;
    private readonly LocalAiPlannerOptions _plannerOptions;

    public OllamaIntentPlanner(
        HttpClient httpClient,
        OllamaProviderOptions? providerOptions = null,
        LocalAiPlannerOptions? plannerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _providerOptions = providerOptions ?? new OllamaProviderOptions();
        _plannerOptions = plannerOptions ?? new LocalAiPlannerOptions();
    }

    /// <summary>The model tag this planner asks Ollama to run.</summary>
    public string ModelTag => _plannerOptions.ModelTag;

    /// <inheritdoc />
    public async Task<LocalAiPlanningResult> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var planSource = $"{ProviderName}:{_plannerOptions.ModelTag}";
        var userText = (request.Text ?? string.Empty).Trim();
        if (userText.Length == 0)
        {
            return LocalAiPlanningResult.Failed("Empty command.", ProviderName, _plannerOptions.ModelTag);
        }
        if (userText.Length > _plannerOptions.MaxPromptChars)
        {
            userText = userText[.._plannerOptions.MaxPromptChars];
        }

        var body = new GenerateBody(
            Model: _plannerOptions.ModelTag,
            System: SystemPrompt,
            Prompt: userText,
            Stream: false,
            Format: "json");

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _plannerOptions.TimeoutSeconds)));

            var url = new Uri(_providerOptions.Endpoint, GeneratePath);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body, options: JsonOptions),
            };

            using var response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return LocalAiPlanningResult.Failed(
                    $"Ollama returned HTTP {(int)response.StatusCode}.",
                    ProviderName,
                    _plannerOptions.ModelTag);
            }

            GenerateResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<GenerateResponse>(JsonOptions, linked.Token)
                    .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return LocalAiPlanningResult.Failed("Ollama response was not valid JSON.", ProviderName, _plannerOptions.ModelTag);
            }

            if (!OllamaPlanJsonParser.TryParse(payload?.Response, request.CorrelationId, planSource, out var plan, out var parseError))
            {
                return LocalAiPlanningResult.Failed(parseError ?? "Model plan failed validation.", ProviderName, _plannerOptions.ModelTag);
            }

            if (plan!.Confidence is { } c && c < _plannerOptions.MinimumConfidence)
            {
                return LocalAiPlanningResult.Failed(
                    $"Model confidence {c:0.00} below floor {_plannerOptions.MinimumConfidence:0.00}.",
                    ProviderName,
                    _plannerOptions.ModelTag);
            }

            return LocalAiPlanningResult.Succeeded(plan, ProviderName, _plannerOptions.ModelTag);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return LocalAiPlanningResult.Failed("Local planning timed out.", ProviderName, _plannerOptions.ModelTag);
        }
#pragma warning disable CA1031 // Any model/transport failure becomes a safe fallback, never a crash.
        catch (Exception ex)
        {
            return LocalAiPlanningResult.Failed($"Local planning failed: {ex.Message}", ProviderName, _plannerOptions.ModelTag);
        }
#pragma warning restore CA1031
    }

    private sealed record GenerateBody(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("format")] string Format);

    private sealed record GenerateResponse(
        [property: JsonPropertyName("response")] string? Response,
        [property: JsonPropertyName("done")] bool Done);
}
