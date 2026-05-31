namespace Vayu.Core.Setup;

/// <summary>
/// In-memory implementation of <see cref="IFirstRunSetupService"/> for M2.5.
/// Holds the wizard's working state for the lifetime of the process; a
/// SQLite-backed persistent implementation arrives in M2.8.
/// </summary>
/// <remarks>
/// This service is intentionally side-effect-free: it never installs Ollama,
/// never pulls a model, never talks to the cloud. All it does is remember
/// what mode the user picked and whether they advanced past the final page.
/// </remarks>
public sealed class InMemoryFirstRunSetupService : IFirstRunSetupService
{
    private readonly IClock _clock;
    private readonly object _lock = new();
    private FirstRunSetupState _state = FirstRunSetupState.Empty;

    public InMemoryFirstRunSetupService(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <inheritdoc />
    public Task<FirstRunSetupState> GetStateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_state);
        }
    }

    /// <inheritdoc />
    public Task<FirstRunSetupState> StartAsync(FirstRunSetupMode mode, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _state = _state with
            {
                Mode = mode,
                Completed = false,
                CompletedAtUtc = null,
            };
            return Task.FromResult(_state);
        }
    }

    /// <inheritdoc />
    public Task<FirstRunSetupState> CompleteAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _state = _state with
            {
                Completed = true,
                CompletedAtUtc = _clock.UtcNow,
            };
            return Task.FromResult(_state);
        }
    }

    /// <inheritdoc />
    public Task ResetAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _state = FirstRunSetupState.Empty;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Test/UI-only convenience to record what the wizard learned about Ollama
    /// during the current session. Never persists secrets and never invokes I/O.
    /// </summary>
    public Task<FirstRunSetupState> UpdateDetectionAsync(
        bool ollamaDetected,
        string? endpoint,
        string? selectedLocalModel,
        bool localModelInstalled,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _state = _state with
            {
                OllamaDetected = ollamaDetected,
                OllamaEndpoint = endpoint,
                SelectedLocalModel = selectedLocalModel,
                LocalModelInstalled = localModelInstalled,
            };
            return Task.FromResult(_state);
        }
    }
}
