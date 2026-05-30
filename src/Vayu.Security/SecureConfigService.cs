namespace Vayu.Security;

/// <summary>
/// Where Vayu found a configured secret, without revealing the value.
/// </summary>
/// <param name="IsConfigured">True if at least one store had the secret.</param>
/// <param name="Source">The <see cref="ISecretStore.SourceName"/> of the store that had it.</param>
public sealed record SecretLookupStatus(bool IsConfigured, string? Source)
{
    /// <summary>Singleton "not configured anywhere".</summary>
    public static SecretLookupStatus NotFound { get; } = new(false, null);
}

/// <summary>
/// Resolves Vayu's known secrets across the configured store priority
/// chain: Windows Credential Manager → environment variable → encrypted
/// local file. The status methods never expose the value;
/// <c>ReadGeminiKeyAsync</c> is the single internal API that returns the
/// raw string and is meant for one purpose — the Gemini HTTP client.
/// </summary>
public sealed class SecureConfigService
{
    /// <summary>Credential Manager target name for the Gemini API key.</summary>
    public const string GeminiApiKeyCredentialName = "Vayu:GeminiApiKey";

    /// <summary>Environment variable Vayu reads for the Gemini API key.</summary>
    public const string GeminiApiKeyEnvironmentVariable = "GEMINI_API_KEY";

    private readonly ISecretStore _credentialStore;
    private readonly ISecretStore _environmentStore;
    private readonly ISecretStore _encryptedStore;

    /// <summary>
    /// Constructs the service with the three priority-ordered stores.
    /// Pass test doubles in unit tests; production wires real implementations.
    /// </summary>
    public SecureConfigService(
        ISecretStore credentialStore,
        ISecretStore environmentStore,
        ISecretStore encryptedStore)
    {
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(environmentStore);
        ArgumentNullException.ThrowIfNull(encryptedStore);
        _credentialStore = credentialStore;
        _environmentStore = environmentStore;
        _encryptedStore = encryptedStore;
    }

    /// <summary>Reports whether a Gemini key is configured and which store holds it. Never returns the value.</summary>
    public async Task<SecretLookupStatus> GetGeminiKeyStatusAsync(CancellationToken cancellationToken = default)
    {
        if (await _credentialStore.ExistsAsync(GeminiApiKeyCredentialName, cancellationToken).ConfigureAwait(false))
        {
            return new SecretLookupStatus(true, _credentialStore.SourceName);
        }

        if (await _environmentStore.ExistsAsync(GeminiApiKeyEnvironmentVariable, cancellationToken).ConfigureAwait(false))
        {
            return new SecretLookupStatus(true, _environmentStore.SourceName);
        }

        if (await _encryptedStore.ExistsAsync(GeminiApiKeyCredentialName, cancellationToken).ConfigureAwait(false))
        {
            return new SecretLookupStatus(true, _encryptedStore.SourceName);
        }

        return SecretLookupStatus.NotFound;
    }

    /// <summary>
    /// Reads the Gemini API key value from the highest-priority store that
    /// has it. CALLER MUST NOT LOG THE RETURN VALUE. Used only by the
    /// Gemini HTTP client at the moment of an authenticated request.
    /// </summary>
    public async Task<string?> ReadGeminiKeyAsync(CancellationToken cancellationToken = default)
    {
        var v = await _credentialStore.GetAsync(GeminiApiKeyCredentialName, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(v))
        {
            return v;
        }

        v = await _environmentStore.GetAsync(GeminiApiKeyEnvironmentVariable, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(v))
        {
            return v;
        }

        return await _encryptedStore.GetAsync(GeminiApiKeyCredentialName, cancellationToken).ConfigureAwait(false);
    }
}
