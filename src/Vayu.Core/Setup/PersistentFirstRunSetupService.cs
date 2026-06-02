using System.Text.Json;

namespace Vayu.Core.Setup;

/// <summary>
/// An <see cref="IFirstRunSetupService"/> that persists setup state durably via
/// injected get/set delegates (backed by a SQLite key/value store in the desktop
/// app). State survives restarts, so the first-run experience shows once and the
/// app routes straight to the Command Center afterwards.
/// </summary>
/// <remarks>
/// The persisted JSON carries setup progress, chosen storage paths, the selected
/// local/Whisper model paths, and boolean readiness flags. It <b>never</b>
/// contains an API key — only <see cref="FirstRunSetupState.GeminiKeyConfigured"/>,
/// a boolean resolved live from the secure secret store. Keeping the persistence
/// behind delegates keeps <c>Vayu.Core</c> free of any storage dependency.
/// </remarks>
public sealed class PersistentFirstRunSetupService : IFirstRunSetupService
{
    /// <summary>The key the state is stored under.</summary>
    public const string StateKey = "firstrun.setup.state";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly Func<string, CancellationToken, Task<string?>> _get;
    private readonly Func<string, string, CancellationToken, Task> _set;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FirstRunSetupState? _cached;

    public PersistentFirstRunSetupService(
        Func<string, CancellationToken, Task<string?>> get,
        Func<string, string, CancellationToken, Task> set,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(clock);
        _get = get;
        _set = set;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<FirstRunSetupState> GetStateAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await LoadLockedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<FirstRunSetupState> StartAsync(FirstRunSetupMode mode, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = await LoadLockedAsync(ct).ConfigureAwait(false);
            var next = current with
            {
                Mode = mode,
                Completed = false,
                Skipped = false,
                CompletedAtUtc = null,
            };
            return await SaveLockedAsync(next, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<FirstRunSetupState> CompleteAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = await LoadLockedAsync(ct).ConfigureAwait(false);
            var next = current with
            {
                Completed = true,
                CompletedAtUtc = _clock.UtcNow,
            };
            return await SaveLockedAsync(next, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SaveLockedAsync(FirstRunSetupState.Empty, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Records an explicit Skip — counts as completed for routing, but flags that
    /// the user did not finish the steps.
    /// </summary>
    public async Task<FirstRunSetupState> SkipAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = await LoadLockedAsync(ct).ConfigureAwait(false);
            var next = current with
            {
                Completed = true,
                Skipped = true,
                CompletedAtUtc = _clock.UtcNow,
            };
            return await SaveLockedAsync(next, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Persists an updated state snapshot (storage paths, selections, flags).</summary>
    public async Task<FirstRunSetupState> UpdateAsync(FirstRunSetupState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await SaveLockedAsync(state, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<FirstRunSetupState> LoadLockedAsync(CancellationToken ct)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        string? json;
        try
        {
            json = await _get(StateKey, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A read failure must never block startup; fall back to Empty.
        catch (Exception)
        {
            return FirstRunSetupState.Empty;
        }
#pragma warning restore CA1031

        if (string.IsNullOrWhiteSpace(json))
        {
            _cached = FirstRunSetupState.Empty;
            return _cached;
        }

        try
        {
            _cached = JsonSerializer.Deserialize<FirstRunSetupState>(json, JsonOptions)
                      ?? FirstRunSetupState.Empty;
        }
        catch (JsonException)
        {
            _cached = FirstRunSetupState.Empty;
        }
        return _cached;
    }

    private async Task<FirstRunSetupState> SaveLockedAsync(FirstRunSetupState state, CancellationToken ct)
    {
        _cached = state;
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await _set(StateKey, json, ct).ConfigureAwait(false);
        return state;
    }
}
