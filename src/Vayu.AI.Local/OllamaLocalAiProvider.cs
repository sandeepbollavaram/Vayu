namespace Vayu.AI.Local;

/// <summary>
/// Adapts <see cref="IOllamaRuntimeService"/> to the milestone-agnostic
/// <see cref="ILocalAiProvider"/> contract from M2.1. Maps Ollama-specific
/// detection booleans into <see cref="LocalAiProviderStatus"/> and filters
/// installed models down to entries that exist in
/// <see cref="LocalModelCatalog"/>.
/// </summary>
/// <remarks>
/// Unknown installed tags (a user pulled <c>mistral-nemo</c> for example)
/// are reported in <see cref="LocalAiProviderStatus.InstalledModels"/>
/// (raw tag list) but excluded from <see cref="ListModelsAsync"/>'s
/// curated list — the wizard only recommends curated entries.
/// </remarks>
public sealed class OllamaLocalAiProvider : ILocalAiProvider
{
    /// <summary>Stable provider id used in audit logs and the AI Provider Registry.</summary>
    public const string ProviderId = "ollama";

    private readonly IOllamaRuntimeService _runtime;

    public OllamaLocalAiProvider(IOllamaRuntimeService runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    /// <inheritdoc />
    public string ProviderName => ProviderId;

    /// <inheritdoc />
    public async Task<LocalAiProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var isInstalled = await _runtime.IsInstalledAsync(cancellationToken).ConfigureAwait(false);
        var isReachable = await _runtime.IsReachableAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<OllamaModelInfo> rawModels = isReachable
            ? await _runtime.ListLocalModelsAsync(cancellationToken).ConfigureAwait(false)
            : Array.Empty<OllamaModelInfo>();

        var installedTags = new List<string>(rawModels.Count);
        foreach (var model in rawModels)
        {
            if (!string.IsNullOrWhiteSpace(model.Tag))
            {
                installedTags.Add(model.Tag);
            }
        }

        var recommendedTag = LocalModelCatalog.Recommended.ModelTag;
        var recommendedPresent = false;
        foreach (var tag in installedTags)
        {
            if (string.Equals(tag, recommendedTag, StringComparison.OrdinalIgnoreCase))
            {
                recommendedPresent = true;
                break;
            }
        }

        return new LocalAiProviderStatus(
            ProviderName: ProviderId,
            IsAvailable: isInstalled || isReachable,
            IsRunning: isReachable,
            Endpoint: _runtime.Endpoint,
            InstalledModels: installedTags,
            RecommendedModelPresent: recommendedPresent,
            Message: BuildMessage(isInstalled, isReachable));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var rawModels = await _runtime.ListLocalModelsAsync(cancellationToken).ConfigureAwait(false);
        if (rawModels.Count == 0)
        {
            return Array.Empty<LocalModelDescriptor>();
        }

        var curated = new List<LocalModelDescriptor>(rawModels.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in rawModels)
        {
            if (string.IsNullOrWhiteSpace(model.Tag))
            {
                continue;
            }
            var descriptor = LocalModelCatalog.FindByTag(model.Tag);
            if (descriptor is not null && seen.Add(descriptor.ModelTag))
            {
                curated.Add(descriptor);
            }
        }
        return curated;
    }

    private static string? BuildMessage(bool isInstalled, bool isReachable)
    {
        if (isReachable)
        {
            return null;
        }
        if (isInstalled)
        {
            return "Ollama is installed but the local server is not running.";
        }
        return "Ollama was not detected on this device.";
    }
}
