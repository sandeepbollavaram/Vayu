namespace Vayu.AI.Local;

/// <summary>
/// Rich detection snapshot returned by
/// <see cref="OllamaRuntimeService.GetRuntimeStatusAsync"/>. The First
/// Run Wizard renders this directly: it needs to know whether the
/// executable was found, whether winget agrees, and whether the server
/// is actually answering — not just the basic <see cref="IOllamaRuntimeService"/>
/// booleans.
/// </summary>
/// <param name="IsExecutableAvailable">True when <c>ollama</c> (or <c>ollama.exe</c>) was found on <c>PATH</c>.</param>
/// <param name="IsWingetPackageDetected">True when <c>winget list --id Ollama.Ollama</c> reported the package. False is non-fatal — winget may not even be present.</param>
/// <param name="IsEndpointReachable">True when <c>GET /api/tags</c> at <see cref="Endpoint"/> returned a success status.</param>
/// <param name="Endpoint">The endpoint that was probed.</param>
/// <param name="Version">Ollama version string when available. Always <see langword="null"/> in M2.2 (filled in later via <c>/api/version</c>).</param>
/// <param name="InstalledModels">Models the server reported. Empty when the endpoint was unreachable.</param>
/// <param name="RecommendedModelPresent">True when <see cref="LocalModelCatalog.Recommended"/>'s tag appears in <see cref="InstalledModels"/>.</param>
/// <param name="Message">Optional short, redaction-safe explanation for the UI (e.g. "Ollama is installed but its server is not running").</param>
public sealed record OllamaRuntimeStatus(
    bool IsExecutableAvailable,
    bool IsWingetPackageDetected,
    bool IsEndpointReachable,
    Uri Endpoint,
    string? Version,
    IReadOnlyList<OllamaModelInfo> InstalledModels,
    bool RecommendedModelPresent,
    string? Message = null)
{
    /// <summary>A safe "nothing detected" snapshot the wizard can render before the first probe completes.</summary>
    public static OllamaRuntimeStatus Unavailable(Uri endpoint, string? message = null)
        => new(
            IsExecutableAvailable: false,
            IsWingetPackageDetected: false,
            IsEndpointReachable: false,
            Endpoint: endpoint,
            Version: null,
            InstalledModels: Array.Empty<OllamaModelInfo>(),
            RecommendedModelPresent: false,
            Message: message);
}
