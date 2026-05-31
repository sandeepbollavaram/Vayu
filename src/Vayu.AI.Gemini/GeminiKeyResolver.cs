using Vayu.AI.Online;
using Vayu.Security;

namespace Vayu.AI.Gemini;

/// <summary>
/// Resolves the Gemini key through <see cref="SecureConfigService"/>, which
/// already implements Vayu's priority chain: Windows Credential Manager →
/// environment variable → DPAPI-encrypted config.
/// </summary>
/// <remarks>
/// This is the only place the raw key is read, and it is read only when a
/// caller is about to make an authenticated request. The value is wrapped in
/// the internal <see cref="GeminiKeyResolutionResult"/> and never logged.
/// (Credential-Manager <i>write</i> from the UI lands in M3.3; this resolver
/// already reads from it.)
/// </remarks>
internal sealed class GeminiKeyResolver : IGeminiKeyResolver
{
    private readonly SecureConfigService _secureConfig;

    public GeminiKeyResolver(SecureConfigService secureConfig)
    {
        ArgumentNullException.ThrowIfNull(secureConfig);
        _secureConfig = secureConfig;
    }

    /// <inheritdoc />
    public async Task<GeminiKeyResolutionResult> ResolveAsync(CancellationToken cancellationToken = default)
    {
        SecretLookupStatus status;
        try
        {
            status = await _secureConfig.GetGeminiKeyStatusAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A secret-store failure must degrade to "not configured", never crash.
        catch (Exception)
        {
            return GeminiKeyResolutionResult.NotConfigured("Could not read the Gemini key store.");
        }
#pragma warning restore CA1031

        if (!status.IsConfigured)
        {
            return GeminiKeyResolutionResult.NotConfigured();
        }

        var value = await _secureConfig.ReadGeminiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(value))
        {
            // Status said configured but the read came back empty — treat as not configured.
            return GeminiKeyResolutionResult.NotConfigured();
        }

        return GeminiKeyResolutionResult.Configured(MapSource(status.Source), value);
    }

    private static OnlineProviderKeySource MapSource(string? sourceName) => sourceName switch
    {
        "WindowsCredentialManager" => OnlineProviderKeySource.WindowsCredentialManager,
        "Environment" => OnlineProviderKeySource.EnvironmentVariable,
        "EncryptedJson" => OnlineProviderKeySource.DpapiEncryptedConfig,
        _ => OnlineProviderKeySource.EnvironmentVariable,
    };
}
