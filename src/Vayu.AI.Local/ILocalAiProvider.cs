namespace Vayu.AI.Local;

/// <summary>
/// Contract for an offline AI provider Vayu can drive end-to-end. M2.1
/// only ships this interface; the first concrete implementation (Ollama)
/// arrives with <c>IOllamaRuntimeService</c>'s real impl in M2.2 and the
/// <c>OllamaAiProvider</c> in M2.7. Cloud providers implement a different
/// contract (M3+).
/// </summary>
/// <remarks>
/// Any plan an <see cref="ILocalAiProvider"/> produces must still pass
/// through the <c>AgentRuntime</c> and <c>IPermissionService</c> before
/// any side-effecting agent runs — AI never invokes a tool directly in
/// Vayu.
/// </remarks>
public interface ILocalAiProvider
{
    /// <summary>Stable provider identifier, e.g. <c>"ollama"</c>.</summary>
    string ProviderName { get; }

    /// <summary>Probes the provider and returns a redaction-safe status snapshot.</summary>
    Task<LocalAiProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the curated catalog entries that are currently installed in the provider's local store.</summary>
    Task<IReadOnlyList<LocalModelDescriptor>> ListModelsAsync(CancellationToken cancellationToken = default);
}
