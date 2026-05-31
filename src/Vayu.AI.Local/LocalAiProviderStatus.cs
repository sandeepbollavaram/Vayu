namespace Vayu.AI.Local;

/// <summary>
/// Snapshot of an offline AI provider's runtime state at a moment in
/// time. Returned by <see cref="ILocalAiProvider.GetStatusAsync"/> and
/// rendered on the First Run Setup Wizard's Ollama page. Carries no
/// secrets, no tokens, no private context — only "is this thing up?".
/// </summary>
/// <param name="ProviderName">Stable identifier for the provider, e.g. <c>"ollama"</c>.</param>
/// <param name="IsAvailable">True when the provider binary is detected (e.g. on PATH).</param>
/// <param name="IsRunning">True when the provider's HTTP endpoint responds successfully.</param>
/// <param name="Endpoint">The endpoint Vayu probed. May be the default or a user override.</param>
/// <param name="InstalledModels">Tags currently in the local model store. Empty when none or when the probe failed.</param>
/// <param name="RecommendedModelPresent">True when <see cref="LocalModelCatalog.Recommended"/> is in <see cref="InstalledModels"/>.</param>
/// <param name="Message">Optional short human-readable note for the UI; already redaction-safe.</param>
public sealed record LocalAiProviderStatus(
    string ProviderName,
    bool IsAvailable,
    bool IsRunning,
    Uri Endpoint,
    IReadOnlyList<string> InstalledModels,
    bool RecommendedModelPresent,
    string? Message = null)
{
    /// <summary>A safe "nothing detected yet" status that the UI can render before any probe runs.</summary>
    public static LocalAiProviderStatus Unavailable(string providerName, Uri endpoint, string? message = null)
        => new(
            ProviderName: providerName,
            IsAvailable: false,
            IsRunning: false,
            Endpoint: endpoint,
            InstalledModels: Array.Empty<string>(),
            RecommendedModelPresent: false,
            Message: message);
}
