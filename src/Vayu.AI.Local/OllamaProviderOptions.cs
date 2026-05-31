namespace Vayu.AI.Local;

/// <summary>
/// Provider-specific configuration for <see cref="OllamaRuntimeService"/>.
/// Top-level offline settings live on <see cref="LocalAiOptions"/>; this
/// record holds the Ollama-only knobs (executable name, winget package id,
/// probe timeout).
/// </summary>
/// <remarks>
/// Defaults match Ollama's documented Windows install. <see cref="ProbeTimeoutSeconds"/>
/// caps every detection round-trip so the First Run Wizard cannot stall on
/// a slow or hanging probe.
/// </remarks>
public sealed record OllamaProviderOptions
{
    /// <summary>The Ollama REST endpoint. Default: <c>http://localhost:11434</c>.</summary>
    public Uri Endpoint { get; init; } = new Uri("http://localhost:11434");

    /// <summary>Executable name searched for on <c>PATH</c>. <c>.exe</c> is appended on Windows.</summary>
    public string ExecutableName { get; init; } = "ollama";

    /// <summary>Winget package id used for the optional secondary probe.</summary>
    public string WingetPackageId { get; init; } = "Ollama.Ollama";

    /// <summary>Hard cap on any single detection probe (HTTP or process). Default 5 s.</summary>
    public int ProbeTimeoutSeconds { get; init; } = 5;
}
