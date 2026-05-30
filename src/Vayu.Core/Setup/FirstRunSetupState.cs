namespace Vayu.Core.Setup;

/// <summary>
/// Snapshot of the First Run Setup Wizard progress. Persisted in the
/// <c>UserSettings</c> SQLite table; read on every app launch to decide
/// whether the wizard reopens.
/// </summary>
/// <param name="Completed">True once the user finished (or explicitly skipped) the wizard at least once.</param>
/// <param name="Mode">The AI mode chosen during setup.</param>
/// <param name="OllamaDetected">True if Ollama was found on this machine during the last setup attempt.</param>
/// <param name="OllamaEndpoint">The Ollama REST endpoint Vayu will use (typically <c>http://localhost:11434</c>).</param>
/// <param name="SelectedLocalModel">The Ollama tag of the local model the user picked, e.g. <c>gemma3:4b</c>.</param>
/// <param name="LocalModelInstalled">True if the selected model is present in the local Ollama store.</param>
/// <param name="GeminiKeyConfigured">True if a Gemini API key is resolvable from any configured secret store. The key value itself is never stored here.</param>
/// <param name="CompletedAtUtc">When the wizard last completed; null if it has not completed yet.</param>
public sealed record FirstRunSetupState(
    bool Completed,
    FirstRunSetupMode Mode,
    bool OllamaDetected,
    string? OllamaEndpoint,
    string? SelectedLocalModel,
    bool LocalModelInstalled,
    bool GeminiKeyConfigured,
    DateTimeOffset? CompletedAtUtc)
{
    /// <summary>
    /// A freshly-installed state before the wizard has ever run.
    /// </summary>
    public static FirstRunSetupState Empty { get; } = new(
        Completed: false,
        Mode: FirstRunSetupMode.OfflineOnly,
        OllamaDetected: false,
        OllamaEndpoint: null,
        SelectedLocalModel: null,
        LocalModelInstalled: false,
        GeminiKeyConfigured: false,
        CompletedAtUtc: null);
}
