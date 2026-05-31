using Vayu.AI.Online;
using Vayu.Security;

namespace Vayu.AI.Gemini;

/// <summary>
/// Implements the Gemini key lifecycle for the Settings UI (M3.3): save to the
/// secure store, report presence (never the value), and remove. Built directly
/// on the three <see cref="ISecretStore"/>s so it can both read and write —
/// <see cref="SecureConfigService"/> is read-only.
/// </summary>
/// <remarks>
/// Storage policy:
/// <list type="bullet">
/// <item><see cref="SaveKeyAsync"/> writes to the Windows Credential Manager store (preferred). The key value lives only in memory and the secure store — never a plaintext file, never a log.</item>
/// <item>An environment-variable key is honoured for <i>status</i> but cannot be removed from Vayu (Vayu does not mutate the user's environment).</item>
/// <item><see cref="RemoveKeyAsync"/> deletes from the writable stores (credential + encrypted) only.</item>
/// <item><see cref="GetKeyStatusAsync"/> returns presence + source — never the value.</item>
/// <item><see cref="ValidateKeyAsync"/> is a cloud call and stays disabled until the consent dialog ships in M3.4.</item>
/// </list>
/// </remarks>
public sealed class GeminiKeySetupService : IGeminiKeySetupService
{
    /// <summary>The catalog/provider id this service manages.</summary>
    public const string ProviderId = "gemini";

    private readonly ISecretStore _credentialStore;
    private readonly ISecretStore _environmentStore;
    private readonly ISecretStore _encryptedStore;

    public GeminiKeySetupService(
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

    /// <inheritdoc />
    public async Task<bool> IsKeyConfiguredAsync(CancellationToken ct = default)
    {
        var status = await GetKeyStatusAsync(ct).ConfigureAwait(false);
        return status.IsConfigured;
    }

    /// <summary>Reports presence + source for the Gemini key. Never returns the value.</summary>
    public async Task<OnlineProviderKeyStatus> GetKeyStatusAsync(CancellationToken ct = default)
    {
        if (await _credentialStore.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName, ct).ConfigureAwait(false))
        {
            return OnlineProviderKeyStatus.Configured(ProviderId, OnlineProviderKeySource.WindowsCredentialManager);
        }
        if (await _environmentStore.ExistsAsync(SecureConfigService.GeminiApiKeyEnvironmentVariable, ct).ConfigureAwait(false))
        {
            return OnlineProviderKeyStatus.Configured(
                ProviderId,
                OnlineProviderKeySource.EnvironmentVariable,
                "Configured via the GEMINI_API_KEY environment variable. Vayu cannot remove environment keys.");
        }
        if (await _encryptedStore.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName, ct).ConfigureAwait(false))
        {
            return OnlineProviderKeyStatus.Configured(ProviderId, OnlineProviderKeySource.DpapiEncryptedConfig);
        }
        return OnlineProviderKeyStatus.NotConfigured(ProviderId);
    }

    /// <inheritdoc />
    public async Task SaveKeyAsync(string apiKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // Preferred: Windows Credential Manager (DPAPI-protected, current user).
        var result = await _credentialStore
            .SetAsync(SecureConfigService.GeminiApiKeyCredentialName, apiKey, ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            // Fall back to the DPAPI-encrypted local config store.
            var fallback = await _encryptedStore
                .SetAsync(SecureConfigService.GeminiApiKeyCredentialName, apiKey, ct)
                .ConfigureAwait(false);
            if (!fallback.Success)
            {
                // Message is already redaction-safe (no key value).
                throw new InvalidOperationException(
                    fallback.ErrorMessage ?? result.ErrorMessage ?? "Could not save the Gemini key to a secure store.");
            }
        }
    }

    /// <inheritdoc />
    public async Task RemoveKeyAsync(CancellationToken ct = default)
    {
        // Remove from the writable stores only. The environment variable is the
        // user's to manage; Vayu never edits it.
        await _credentialStore.DeleteAsync(SecureConfigService.GeminiApiKeyCredentialName, ct).ConfigureAwait(false);
        await _encryptedStore.DeleteAsync(SecureConfigService.GeminiApiKeyCredentialName, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Validating a key is a cloud call and requires the consent dialog that
    /// ships in M3.4. Until then this is intentionally unsupported.
    /// </summary>
    public Task<bool> ValidateKeyAsync(CancellationToken ct = default)
        => throw new NotSupportedException("Key validation is a cloud call and is enabled in M3.4 (consent dialog).");

    /// <summary>True when the configured key lives in a store Vayu can delete (i.e. not the environment).</summary>
    public async Task<bool> CanRemoveKeyAsync(CancellationToken ct = default)
    {
        if (await _credentialStore.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName, ct).ConfigureAwait(false))
        {
            return true;
        }
        return await _encryptedStore.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName, ct).ConfigureAwait(false);
    }
}
