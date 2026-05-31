using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vayu.AI.Local;

/// <summary>
/// First real implementation of <see cref="IOllamaRuntimeService"/>.
/// Three safety-bounded operations from the M1 contract plus an
/// M2.2 enrichment, <see cref="GetRuntimeStatusAsync"/>, that drives
/// the First Run Setup Wizard:
/// <list type="bullet">
/// <item><see cref="IsInstalledAsync"/> walks <c>PATH</c> for the Ollama executable.</item>
/// <item><see cref="IsReachableAsync"/> sends a single <c>GET /api/tags</c> bounded by <see cref="OllamaProviderOptions.ProbeTimeoutSeconds"/>.</item>
/// <item><see cref="ListLocalModelsAsync"/> parses the same response into <see cref="OllamaModelInfo"/>.</item>
/// <item><see cref="GetRuntimeStatusAsync"/> aggregates the above and adds an optional <c>winget</c> probe.</item>
/// </list>
/// </summary>
/// <remarks>
/// Every method swallows transport / parse failures and returns a safe
/// default (<c>false</c> / empty list) — Ollama not running must not
/// crash the wizard. User cancellation is honoured; timeouts are not.
///
/// M2.2 does NOT pull models, install Ollama, or talk to the cloud.
/// That work belongs to M2.6 and must require explicit user consent.
/// </remarks>
public sealed class OllamaRuntimeService : IOllamaRuntimeService
{
    /// <summary>The Ollama HTTP API path that lists locally available models.</summary>
    public const string TagsPath = "api/tags";

    private static readonly JsonSerializerOptions JsonReadOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OllamaProviderOptions _options;
    private readonly Func<CancellationToken, Task<bool>> _wingetProbe;

    /// <summary>Standard constructor. The default winget probe shells out to <c>winget list --id &lt;package&gt;</c>.</summary>
    public OllamaRuntimeService(HttpClient httpClient, OllamaProviderOptions? options = null)
        : this(httpClient, options, wingetProbe: null)
    {
    }

    /// <summary>Test-friendly constructor accepting a stub winget probe so unit tests never shell out.</summary>
    public OllamaRuntimeService(
        HttpClient httpClient,
        OllamaProviderOptions? options,
        Func<CancellationToken, Task<bool>>? wingetProbe)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _options = options ?? new OllamaProviderOptions();
        _wingetProbe = wingetProbe ?? DefaultWingetProbeAsync;
    }

    /// <inheritdoc />
    public Uri Endpoint => _options.Endpoint;

    /// <inheritdoc />
    public Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return Task.FromResult(false);
        }

        var isWindows = OperatingSystem.IsWindows();
        var separator = isWindows ? ';' : ':';
        var candidate = isWindows ? _options.ExecutableName + ".exe" : _options.ExecutableName;

        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(Path.Combine(dir.Trim(), candidate)))
                {
                    return Task.FromResult(true);
                }
            }
            catch (IOException)
            {
                // Skip unreachable directories — never throw out of a probe.
            }
            catch (UnauthorizedAccessException)
            {
                // Same — skip and continue scanning PATH.
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendTagsRequestAsync(cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Timeout fired through the linked CTS; treat as unreachable.
            return false;
        }
#pragma warning disable CA1031 // Probe must never throw transport errors at callers.
        catch
        {
            return false;
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OllamaModelInfo>> ListLocalModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendTagsRequestAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<OllamaModelInfo>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            TagsResponse? payload;
            try
            {
                payload = await JsonSerializer.DeserializeAsync<TagsResponse>(stream, JsonReadOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return Array.Empty<OllamaModelInfo>();
            }

            if (payload?.Models is null || payload.Models.Count == 0)
            {
                return Array.Empty<OllamaModelInfo>();
            }

            var results = new List<OllamaModelInfo>(payload.Models.Count);
            foreach (var model in payload.Models)
            {
                if (string.IsNullOrWhiteSpace(model.Name))
                {
                    continue;
                }
                results.Add(new OllamaModelInfo(
                    Name: SplitFamily(model.Name),
                    Tag: model.Name,
                    SizeBytes: model.Size,
                    IsPresent: true));
            }
            return results;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            return Array.Empty<OllamaModelInfo>();
        }
#pragma warning disable CA1031
        catch
        {
            return Array.Empty<OllamaModelInfo>();
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Aggregates the three basic probes and adds an optional winget probe.
    /// This is the snapshot the First Run Setup Wizard renders on its
    /// "Ollama" page.
    /// </summary>
    public async Task<OllamaRuntimeStatus> GetRuntimeStatusAsync(CancellationToken cancellationToken = default)
    {
        var isExecutable = await IsInstalledAsync(cancellationToken).ConfigureAwait(false);
        var isWinget = await _wingetProbe(cancellationToken).ConfigureAwait(false);
        var isReachable = await IsReachableAsync(cancellationToken).ConfigureAwait(false);

        var models = isReachable
            ? await ListLocalModelsAsync(cancellationToken).ConfigureAwait(false)
            : Array.Empty<OllamaModelInfo>();

        var recommendedTag = LocalModelCatalog.Recommended.ModelTag;
        var recommendedPresent = false;
        foreach (var model in models)
        {
            if (string.Equals(model.Tag, recommendedTag, StringComparison.OrdinalIgnoreCase))
            {
                recommendedPresent = true;
                break;
            }
        }

        string? message = null;
        if (!isReachable && (isExecutable || isWinget))
        {
            message = "Ollama is installed but its server is not running. Try 'ollama serve' or restart Ollama.";
        }
        else if (!isExecutable && !isWinget && !isReachable)
        {
            message = "Ollama was not detected on this device.";
        }

        return new OllamaRuntimeStatus(
            IsExecutableAvailable: isExecutable,
            IsWingetPackageDetected: isWinget,
            IsEndpointReachable: isReachable,
            Endpoint: _options.Endpoint,
            Version: null,
            InstalledModels: models,
            RecommendedModelPresent: recommendedPresent,
            Message: message);
    }

    private async Task<HttpResponseMessage> SendTagsRequestAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ProbeTimeoutSeconds)));

        var url = new Uri(_options.Endpoint, TagsPath);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
    }

    private async Task<bool> DefaultWingetProbeAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var psi = new ProcessStartInfo("winget", $"list --id {_options.WingetPackageId} --accept-source-agreements")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.ProbeTimeoutSeconds)));

            try
            {
                await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return false;
            }

            if (process.ExitCode != 0)
            {
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return output.Contains(_options.WingetPackageId, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            return false;
        }
#pragma warning disable CA1031
        catch
        {
            // winget missing / locked / refusing — fall back to "not detected via winget".
            return false;
        }
#pragma warning restore CA1031
    }

    private static string SplitFamily(string tag)
    {
        var idx = tag.IndexOf(':', StringComparison.Ordinal);
        return idx > 0 ? tag[..idx] : tag;
    }

    private sealed record TagsResponse(
        [property: JsonPropertyName("models")] IReadOnlyList<TagModel>? Models);

    private sealed record TagModel(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("size")] long? Size);
}
