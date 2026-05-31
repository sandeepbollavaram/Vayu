using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Vayu.AI.Online;
using Vayu.Core;
using Vayu.Security;

namespace Vayu.AI.Gemini;

/// <summary>
/// First concrete <see cref="IOnlineAiProvider"/> — Google Gemini. Plans a
/// command into a validated <see cref="IntentPlan"/>, but only after the user
/// has consented to this specific cloud call.
/// </summary>
/// <remarks>
/// Safety posture (M3.2):
/// <list type="bullet">
/// <item>Off by default; reached only when the user enabled online AI and selected Gemini.</item>
/// <item><see cref="PlanAsync"/> makes <b>no HTTP call</b> unless consent is <see cref="CloudConsentDecision.AllowOnce"/> AND a key is configured.</item>
/// <item>The key is read from <see cref="SecureConfigService"/> at the moment of the request, passed straight to the HTTP layer, and never logged or returned.</item>
/// <item>Gemini produces a plan only; it never executes. The plan still flows through AgentRuntime → IPermissionService → agent → audit.</item>
/// <item>Output is allowlist- and risk-validated via <see cref="OnlineAiSafetyPolicy"/>.</item>
/// </list>
/// </remarks>
public sealed class GeminiProvider : IOnlineAiProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GeminiProviderOptions _options;
    private readonly IGeminiKeyResolver _keyResolver;

    /// <summary>Production constructor — resolves the key via <see cref="SecureConfigService"/>.</summary>
    public GeminiProvider(HttpClient httpClient, SecureConfigService secureConfig, GeminiProviderOptions? options = null)
        : this(httpClient, new GeminiKeyResolver(secureConfig ?? throw new ArgumentNullException(nameof(secureConfig))), options)
    {
    }

    /// <summary>Test constructor — accepts a stub key resolver so unit tests supply no real key.</summary>
    internal GeminiProvider(HttpClient httpClient, IGeminiKeyResolver keyResolver, GeminiProviderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(keyResolver);
        _httpClient = httpClient;
        _keyResolver = keyResolver;
        _options = options ?? new GeminiProviderOptions();
    }

    /// <inheritdoc />
    public string ProviderId => _options.ProviderId;

    /// <inheritdoc />
    public async Task<OnlineProviderKeyStatus> GetKeyStatusAsync(CancellationToken cancellationToken = default)
    {
        var resolution = await _keyResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        return resolution.ToKeyStatus(ProviderId);
    }

    /// <inheritdoc />
    public async Task<OnlineAiPlanningResult> PlanAsync(
        CommandRequest request,
        CloudConsentDecision consent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Gate 1: consent. No send on Cancel or UseLocalInstead.
        if (consent != CloudConsentDecision.AllowOnce)
        {
            return OnlineAiPlanningResult.Failed(
                consent == CloudConsentDecision.UseLocalInstead
                    ? "User chose the local model instead of the cloud."
                    : "Cloud call was not authorised.",
                ProviderId,
                _options.ModelName);
        }

        var userText = (request.Text ?? string.Empty).Trim();
        if (userText.Length == 0)
        {
            return OnlineAiPlanningResult.Failed("Empty command.", ProviderId, _options.ModelName);
        }

        // Gate 2: key. No send without a configured key.
        var resolution = await _keyResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (!resolution.IsConfigured || string.IsNullOrEmpty(resolution.KeyValue))
        {
            return OnlineAiPlanningResult.Failed("No Gemini API key is configured.", ProviderId, _options.ModelName);
        }

        var prompt = GeminiPromptBuilder.BuildUserPrompt(userText, _options.MaxPromptChars);
        var planSource = $"{ProviderId}:{_options.ModelName}";

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

            using var httpRequest = BuildRequest(prompt, resolution.KeyValue!);
            using var response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // Never include the response body or key in the message.
                return OnlineAiPlanningResult.Failed(
                    $"Gemini returned HTTP {(int)response.StatusCode}.", ProviderId, _options.ModelName);
            }

            GeminiResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<GeminiResponse>(JsonOptions, linked.Token)
                    .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return OnlineAiPlanningResult.Failed("Gemini response was not valid JSON.", ProviderId, _options.ModelName);
            }

            var modelText = ExtractText(payload);
            if (!GeminiPlanJsonParser.TryParse(modelText, request.CorrelationId, planSource, out var plan, out var parseError))
            {
                return OnlineAiPlanningResult.Failed(parseError ?? "Model plan failed validation.", ProviderId, _options.ModelName);
            }

            if (plan!.Confidence is { } c && c < _options.MinimumConfidence)
            {
                return OnlineAiPlanningResult.Failed(
                    $"Model confidence {c:0.00} below floor {_options.MinimumConfidence:0.00}.",
                    ProviderId, _options.ModelName);
            }

            return OnlineAiPlanningResult.Succeeded(plan, ProviderId, _options.ModelName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return OnlineAiPlanningResult.Failed("Cloud planning timed out.", ProviderId, _options.ModelName);
        }
#pragma warning disable CA1031 // Any transport/model failure becomes a safe failed result, never a crash.
        catch (Exception ex)
        {
            return OnlineAiPlanningResult.Failed($"Cloud planning failed: {ex.Message}", ProviderId, _options.ModelName);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Sends a minimal, redacted health-check to Gemini to confirm the configured
    /// key is accepted. M3.4: gated by the same <see cref="CloudConsentDecision.AllowOnce"/>
    /// rule as planning — no HTTP unless the user consented and a key is configured.
    /// </summary>
    /// <remarks>
    /// The request contains no private user context — a single fixed token. The
    /// key is read at request time and never logged; HTTP error bodies are never
    /// surfaced. Auth (401/403), rate-limit (429), and server (5xx) failures all
    /// return a safe message rather than throwing.
    /// </remarks>
    public async Task<GeminiKeyTestResult> TestKeyAsync(
        CloudConsentDecision consent,
        CancellationToken cancellationToken = default)
    {
        if (consent != CloudConsentDecision.AllowOnce)
        {
            return GeminiKeyTestResult.Failed("Cloud test was not authorised.");
        }

        var resolution = await _keyResolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        if (!resolution.IsConfigured || string.IsNullOrEmpty(resolution.KeyValue))
        {
            return GeminiKeyTestResult.Failed("No Gemini API key is configured.");
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

            using var httpRequest = BuildMinimalRequest(resolution.KeyValue!);
            using var response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return GeminiKeyTestResult.Ok();
            }

            // Map status codes to safe messages — never include the response body.
            var code = (int)response.StatusCode;
            return code switch
            {
                401 or 403 => GeminiKeyTestResult.Failed("Test failed: the key was rejected (invalid or revoked)."),
                429 => GeminiKeyTestResult.Failed("Test failed: rate limited. Try again shortly."),
                >= 500 => GeminiKeyTestResult.Failed("Test failed: the provider returned a server error."),
                _ => GeminiKeyTestResult.Failed($"Test failed: provider returned HTTP {code}."),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return GeminiKeyTestResult.Failed("Test failed: the request timed out.");
        }
#pragma warning disable CA1031 // Any transport failure becomes a safe message, never a crash or key leak.
        catch (Exception)
        {
            return GeminiKeyTestResult.Failed("Test failed: could not reach the provider.");
        }
#pragma warning restore CA1031
    }

    private HttpRequestMessage BuildMinimalRequest(string apiKey)
    {
        // A single fixed token — no system instruction, no user context.
        var url = new Uri(_options.Endpoint, $"{_options.ModelName}:generateContent?key={Uri.EscapeDataString(apiKey)}");
        var body = new GeminiRequest(
            SystemInstruction: new Content(new[] { new Part("ping") }),
            Contents: new[] { new Content(new[] { new Part("ping") }) },
            GenerationConfig: new GenConfig("text/plain"));
        return new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
    }

    private HttpRequestMessage BuildRequest(string prompt, string apiKey)
    {
        // Gemini auth uses the key as a query parameter. It is placed here only,
        // at request time, and never logged. (A future connector may move it to
        // the x-goog-api-key header; both keep the key out of our logs.)
        var url = new Uri(_options.Endpoint, $"{_options.ModelName}:generateContent?key={Uri.EscapeDataString(apiKey)}");

        var body = new GeminiRequest(
            SystemInstruction: new Content(new[] { new Part(GeminiPromptBuilder.SystemInstruction) }),
            Contents: new[] { new Content(new[] { new Part(prompt) }) },
            GenerationConfig: new GenConfig("application/json"));

        return new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
    }

    private static string? ExtractText(GeminiResponse? payload)
        => payload?.Candidates is { Count: > 0 } cands
            ? cands[0].Content?.Parts is { Count: > 0 } parts ? parts[0].Text : null
            : null;

    // ---- Gemini wire DTOs ----

    private sealed record GeminiRequest(
        [property: JsonPropertyName("systemInstruction")] Content SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<Content> Contents,
        [property: JsonPropertyName("generationConfig")] GenConfig GenerationConfig);

    private sealed record Content(
        [property: JsonPropertyName("parts")] IReadOnlyList<Part> Parts);

    private sealed record Part(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record GenConfig(
        [property: JsonPropertyName("responseMimeType")] string ResponseMimeType);

    private sealed record GeminiResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<Candidate>? Candidates);

    private sealed record Candidate(
        [property: JsonPropertyName("content")] Content? Content);
}
