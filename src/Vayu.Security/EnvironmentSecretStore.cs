namespace Vayu.Security;

/// <summary>
/// Read-only secret store backed by process environment variables.
/// Writing or deleting is intentionally not supported — Vayu does not
/// silently mutate the user's environment.
/// </summary>
public sealed class EnvironmentSecretStore : ISecretStore
{
    private const string ReadOnlyMessage = "EnvironmentSecretStore is read-only.";

    /// <inheritdoc />
    public string SourceName => "Environment";

    /// <inheritdoc />
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = Environment.GetEnvironmentVariable(name);
        return Task.FromResult(string.IsNullOrEmpty(value) ? null : value);
    }

    /// <inheritdoc />
    public Task<SecretStoreResult> SetAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return Task.FromResult(SecretStoreResult.Failure(ReadOnlyMessage));
    }

    /// <inheritdoc />
    public Task<SecretStoreResult> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.FromResult(SecretStoreResult.Failure(ReadOnlyMessage));
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = Environment.GetEnvironmentVariable(name);
        return Task.FromResult(!string.IsNullOrEmpty(value));
    }
}
