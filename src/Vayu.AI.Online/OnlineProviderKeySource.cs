namespace Vayu.AI.Online;

/// <summary>
/// Where a provider's API key is resolved from, in Vayu's preference order.
/// This enum names the <i>source</i> only — it never carries the key value.
/// </summary>
/// <remarks>
/// Resolution order (most to least preferred): Windows Credential Manager →
/// environment variable → DPAPI-encrypted local config. A provider with no
/// resolvable key is <see cref="NotConfigured"/>. Keys are never committed,
/// never logged, and never shown after saving.
/// </remarks>
public enum OnlineProviderKeySource
{
    /// <summary>No key is resolvable for this provider.</summary>
    NotConfigured = 0,

    /// <summary>Resolved from Windows Credential Manager (preferred).</summary>
    WindowsCredentialManager = 1,

    /// <summary>Resolved from a user/account environment variable.</summary>
    EnvironmentVariable = 2,

    /// <summary>Resolved from DPAPI-encrypted local config (fallback).</summary>
    DpapiEncryptedConfig = 3,
}
