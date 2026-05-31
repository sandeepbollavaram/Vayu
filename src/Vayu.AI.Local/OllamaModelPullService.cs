using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vayu.AI.Local;

/// <summary>
/// Streams Ollama's <c>POST /api/pull</c> for a curated tag and emits
/// progress to an <see cref="IProgress{T}"/>. M2.6 ships this as the
/// first side-effecting offline flow — every pull happens behind an
/// explicit consent dialog in the First Run Setup Wizard.
/// </summary>
/// <remarks>
/// Hard rules enforced here, not just in the UI:
/// <list type="bullet">
/// <item>The tag MUST appear in <see cref="LocalModelCatalog"/>. Unknown tags are rejected before any HTTP call.</item>
/// <item>No Ollama install is attempted — only the local <c>/api/pull</c> endpoint is touched.</item>
/// <item>No cloud calls, no telemetry, no subprocesses.</item>
/// <item>Cancellation aborts the in-flight stream and emits a final
/// <see cref="OllamaModelPullProgress"/> with <see cref="OllamaModelPullProgress.IsCancelled"/> = <see langword="true"/>.</item>
/// </list>
/// </remarks>
public sealed class OllamaModelPullService : IOllamaModelPullService
{
    /// <summary>The Ollama HTTP API path that pulls a model.</summary>
    public const string PullPath = "api/pull";

    private static readonly JsonSerializerOptions JsonReadOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OllamaProviderOptions _options;

    public OllamaModelPullService(HttpClient httpClient, OllamaProviderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _options = options ?? new OllamaProviderOptions();
    }

    /// <inheritdoc />
    public async Task<OllamaModelPullProgress> PullModelAsync(
        OllamaModelPullRequest request,
        IProgress<OllamaModelPullProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Hard validation: refuse anything outside the curated catalog. The
        // wizard prevents this in the UI, but the service must also refuse —
        // defence in depth.
        if (string.IsNullOrWhiteSpace(request.ModelTag) || LocalModelCatalog.FindByTag(request.ModelTag) is null)
        {
            var rejection = new OllamaModelPullProgress(
                ModelTag: request.ModelTag ?? string.Empty,
                Status: "rejected",
                CompletedBytes: null,
                TotalBytes: null,
                IsComplete: false,
                ErrorMessage: "Refused: model tag is not in the curated catalog.");
            progress?.Report(rejection);
            return rejection;
        }

        var url = new Uri(_options.Endpoint, PullPath);
        var body = new PullBody(request.ModelTag, request.StreamProgress);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body, options: JsonReadOptions),
        };

        OllamaModelPullProgress last = new(
            ModelTag: request.ModelTag,
            Status: "starting",
            CompletedBytes: null,
            TotalBytes: null,
            IsComplete: false);
        progress?.Report(last);

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                last = last with
                {
                    Status = "error",
                    ErrorMessage = $"Ollama returned HTTP {(int)response.StatusCode}.",
                };
                progress?.Report(last);
                return last;
            }

            if (!request.StreamProgress)
            {
                // Non-streaming: one JSON object describing the final state.
                PullLine? single;
                try
                {
                    single = await response.Content
                        .ReadFromJsonAsync<PullLine>(JsonReadOptions, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (JsonException ex)
                {
                    last = last with { Status = "error", ErrorMessage = $"Malformed Ollama response: {ex.Message}" };
                    progress?.Report(last);
                    return last;
                }

                last = MergeLine(last, single);
                if (last.ErrorMessage is null)
                {
                    last = last with { IsComplete = true, Status = string.IsNullOrWhiteSpace(last.Status) ? "success" : last.Status };
                }
                progress?.Report(last);
                return last;
            }

            // Streaming: newline-delimited JSON.
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                if (line is null)
                {
                    break;
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                PullLine? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<PullLine>(line, JsonReadOptions);
                }
                catch (JsonException)
                {
                    // One malformed line must not kill the pull. Skip and keep streaming.
                    continue;
                }

                last = MergeLine(last, parsed);
                progress?.Report(last);

                if (last.ErrorMessage is not null)
                {
                    return last;
                }
                if (string.Equals(parsed?.Status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    last = last with { IsComplete = true };
                    progress?.Report(last);
                    return last;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                last = last with { IsCancelled = true, Status = string.IsNullOrEmpty(last.Status) ? "cancelled" : last.Status };
                progress?.Report(last);
                return last;
            }

            // Stream ended without an explicit success line — treat as complete only if Ollama did not error.
            if (last.ErrorMessage is null)
            {
                last = last with { IsComplete = true };
                progress?.Report(last);
            }
            return last;
        }
        catch (OperationCanceledException)
        {
            last = last with { IsCancelled = true, Status = "cancelled" };
            progress?.Report(last);
            return last;
        }
#pragma warning disable CA1031 // Pull failures must surface to the user, not the call stack.
        catch (Exception ex)
        {
            last = last with { Status = "error", ErrorMessage = ex.Message };
            progress?.Report(last);
            return last;
        }
#pragma warning restore CA1031
        finally
        {
            response?.Dispose();
        }
    }

    private static OllamaModelPullProgress MergeLine(OllamaModelPullProgress current, PullLine? line)
    {
        if (line is null)
        {
            return current;
        }
        return current with
        {
            Status = string.IsNullOrWhiteSpace(line.Status) ? current.Status : line.Status!,
            CompletedBytes = line.Completed ?? current.CompletedBytes,
            TotalBytes = line.Total ?? current.TotalBytes,
            ErrorMessage = string.IsNullOrWhiteSpace(line.Error) ? current.ErrorMessage : line.Error,
        };
    }

    private sealed record PullBody(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record PullLine(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("completed")] long? Completed,
        [property: JsonPropertyName("total")] long? Total,
        [property: JsonPropertyName("digest")] string? Digest,
        [property: JsonPropertyName("error")] string? Error);
}
