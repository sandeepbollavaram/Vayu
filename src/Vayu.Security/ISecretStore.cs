namespace Vayu.Security;

/// <summary>
/// A named source of secrets (Windows Credential Manager, environment
/// variables, encrypted local file, etc). Implementations expose a single
/// flat namespace of secret <em>names</em>; the underlying physical
/// representation is the implementation's concern.
/// </summary>
/// <remarks>
/// Callers MUST NOT log the string returned by <see cref="GetAsync"/>.
/// Pass it to the consuming component (HTTP client, SMTP sender, etc.)
/// and let it go out of scope. The <see cref="Vayu.Security.SecretRedactor"/>
/// is the last line of defence — write code that never produces the value
/// in the first place.
/// </remarks>
public interface ISecretStore
{
    /// <summary>Human-readable name of this store, e.g. <c>"WindowsCredentialManager"</c>.</summary>
    string SourceName { get; }

    /// <summary>Returns the secret value, or <see langword="null"/> if it does not exist.</summary>
    Task<string?> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Writes (or overwrites) the secret. Read-only stores return a failed <see cref="SecretStoreResult"/>.</summary>
    Task<SecretStoreResult> SetAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes the secret. No-op for already-missing entries; returns success.</summary>
    Task<SecretStoreResult> DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>True if a secret with this name exists in the store.</summary>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
}
