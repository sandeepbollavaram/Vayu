using Vayu.AI.Online;

namespace Vayu.AI.Gemini;

/// <summary>
/// Internal result of resolving the Gemini key. The key <b>value</b> is
/// carried only inside this assembly and only for the moment of an
/// authenticated HTTP request — it is never surfaced through
/// <see cref="OnlineProviderKeyStatus"/> or any public type, and this record
/// is <c>internal</c> so it cannot escape <c>Vayu.AI.Gemini</c>.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> is overridden to redact the value so an accidental
/// log/interpolation of the result never prints the secret.
/// </remarks>
internal sealed record GeminiKeyResolutionResult
{
    private GeminiKeyResolutionResult(bool isConfigured, OnlineProviderKeySource source, string? keyValue, string? message)
    {
        IsConfigured = isConfigured;
        Source = source;
        KeyValue = keyValue;
        Message = message;
    }

    /// <summary>True when a key was resolved from some store.</summary>
    public bool IsConfigured { get; }

    /// <summary>Which store the key came from (or <see cref="OnlineProviderKeySource.NotConfigured"/>).</summary>
    public OnlineProviderKeySource Source { get; }

    /// <summary>The raw key — internal use only, never logged, never surfaced. Null when not configured.</summary>
    public string? KeyValue { get; }

    /// <summary>Optional redaction-safe message.</summary>
    public string? Message { get; }

    /// <summary>Builds a "no key" result.</summary>
    public static GeminiKeyResolutionResult NotConfigured(string? message = null)
        => new(false, OnlineProviderKeySource.NotConfigured, keyValue: null,
               message ?? "No Gemini API key configured.");

    /// <summary>Builds a configured result. <paramref name="keyValue"/> stays inside this assembly.</summary>
    public static GeminiKeyResolutionResult Configured(OnlineProviderKeySource source, string keyValue, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyValue);
        if (source == OnlineProviderKeySource.NotConfigured)
        {
            throw new ArgumentException("A configured result cannot use the NotConfigured source.", nameof(source));
        }
        return new GeminiKeyResolutionResult(true, source, keyValue, message);
    }

    /// <summary>Projects to a public, value-free status object safe to log and bind to UI.</summary>
    public OnlineProviderKeyStatus ToKeyStatus(string providerId)
        => IsConfigured
            ? OnlineProviderKeyStatus.Configured(providerId, Source, Message)
            : OnlineProviderKeyStatus.NotConfigured(providerId, Message);

    /// <summary>Redacted — never prints the key.</summary>
    public override string ToString()
        => $"GeminiKeyResolutionResult {{ IsConfigured = {IsConfigured}, Source = {Source}, KeyValue = <redacted> }}";
}
