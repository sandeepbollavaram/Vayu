namespace Vayu.AI.Online;

/// <summary>
/// Whether a provider's key is configured and where it comes from — <b>never
/// the key value itself</b>. Safe to log, bind to UI, and persist.
/// </summary>
/// <param name="ProviderId">The provider this status describes.</param>
/// <param name="IsConfigured">True when a key is resolvable from some <see cref="OnlineProviderKeySource"/>.</param>
/// <param name="Source">The resolved source, or <see cref="OnlineProviderKeySource.NotConfigured"/>.</param>
/// <param name="Message">Optional short, redaction-safe explanation for the UI.</param>
public sealed record OnlineProviderKeyStatus(
    string ProviderId,
    bool IsConfigured,
    OnlineProviderKeySource Source,
    string? Message = null)
{
    /// <summary>A safe "no key" status for a provider.</summary>
    public static OnlineProviderKeyStatus NotConfigured(string providerId, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return new OnlineProviderKeyStatus(
            providerId,
            IsConfigured: false,
            Source: OnlineProviderKeySource.NotConfigured,
            Message: message ?? "No API key configured for this provider.");
    }

    /// <summary>A configured status naming only the source — never the secret.</summary>
    public static OnlineProviderKeyStatus Configured(string providerId, OnlineProviderKeySource source, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        if (source == OnlineProviderKeySource.NotConfigured)
        {
            throw new ArgumentException("A configured status cannot use the NotConfigured source.", nameof(source));
        }
        return new OnlineProviderKeyStatus(providerId, IsConfigured: true, source, message);
    }
}
