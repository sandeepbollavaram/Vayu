namespace Vayu.Voice;

/// <summary>
/// Whether a TTS provider is available and enabled. Reports status only — no
/// audio, no secret.
/// </summary>
/// <param name="ProviderName">The provider this status describes, e.g. <c>"system"</c>.</param>
/// <param name="IsAvailable">True when the provider's speech engine is usable on this machine.</param>
/// <param name="IsEnabled">True when the user has opted into spoken responses.</param>
/// <param name="VoiceName">Active voice name, or null for the OS default.</param>
/// <param name="Message">Short, redaction-safe explanation for the UI.</param>
public sealed record TextToSpeechProviderStatus(
    string ProviderName,
    bool IsAvailable,
    bool IsEnabled,
    string? VoiceName,
    string Message)
{
    /// <summary>A safe "off / not enabled" status.</summary>
    public static TextToSpeechProviderStatus Disabled(string providerName, bool isAvailable, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return new TextToSpeechProviderStatus(
            providerName,
            isAvailable,
            IsEnabled: false,
            VoiceName: null,
            Message: message ?? "Text-to-speech is off by default.");
    }

    /// <summary>An "enabled and ready to speak" status.</summary>
    public static TextToSpeechProviderStatus Enabled(string providerName, string? voiceName = null, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return new TextToSpeechProviderStatus(
            providerName,
            IsAvailable: true,
            IsEnabled: true,
            VoiceName: voiceName,
            Message: message ?? "Spoken responses are on.");
    }
}
