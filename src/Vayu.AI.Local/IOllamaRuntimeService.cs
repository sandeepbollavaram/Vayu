namespace Vayu.AI.Local;

/// <summary>
/// Inspects the local Ollama installation without ever installing software
/// or pulling models on its own. M1 ships only the contract; the real
/// implementation (winget probe, PATH probe, <c>/api/tags</c> ping, pull
/// with progress events) lands in Milestone 2.
/// </summary>
public interface IOllamaRuntimeService
{
    /// <summary>
    /// The Ollama REST endpoint Vayu will talk to (typically <c>http://localhost:11434</c>).
    /// </summary>
    Uri Endpoint { get; }

    /// <summary>
    /// True if Ollama appears to be installed (winget package present, or
    /// the <c>ollama</c> executable is on PATH). Does not start any process.
    /// </summary>
    Task<bool> IsInstalledAsync(CancellationToken ct = default);

    /// <summary>
    /// True if a GET against <see cref="Endpoint"/><c>/api/tags</c> succeeds.
    /// Used by the First Run Wizard's verification step.
    /// </summary>
    Task<bool> IsReachableAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists models currently in the local Ollama store. Used to decide
    /// whether the user's selected model still needs to be pulled.
    /// </summary>
    Task<IReadOnlyList<OllamaModelInfo>> ListLocalModelsAsync(CancellationToken ct = default);
}
