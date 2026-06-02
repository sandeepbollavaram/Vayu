namespace Vayu.Memory;

/// <summary>
/// A durable, local-only key/value store for small user-setting values
/// (first-run setup state, chosen paths, selections). Values are plain strings
/// (typically JSON). <b>Never</b> store secrets here — API keys live in the
/// secure secret store, not in this database.
/// </summary>
public interface IUserSettingsStore
{
    /// <summary>Returns the value for <paramref name="key"/>, or null when absent.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Inserts or replaces the value for <paramref name="key"/>.</summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes <paramref name="key"/> if present.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
