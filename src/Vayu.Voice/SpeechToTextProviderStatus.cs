namespace Vayu.Voice;

/// <summary>
/// Whether a local STT provider is available and configured. Reports status
/// only — it never holds audio and never opens the microphone.
/// </summary>
/// <param name="ProviderName">The provider this status describes, e.g. <c>"whispercpp"</c>.</param>
/// <param name="IsAvailable">True when the provider's runtime is present/usable on this machine.</param>
/// <param name="IsConfigured">True when a model is configured and the provider can transcribe.</param>
/// <param name="ModelPath">The configured model path, or null when none is set.</param>
/// <param name="Message">Short, redaction-safe explanation for the UI.</param>
public sealed record SpeechToTextProviderStatus(
    string ProviderName,
    bool IsAvailable,
    bool IsConfigured,
    string? ModelPath,
    string Message)
{
    /// <summary>A safe "not configured" status.</summary>
    public static SpeechToTextProviderStatus NotConfigured(string providerName, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return new SpeechToTextProviderStatus(
            providerName,
            IsAvailable: false,
            IsConfigured: false,
            ModelPath: null,
            Message: message ?? "Local STT provider is not configured yet.");
    }

    /// <summary>A "ready to transcribe" status.</summary>
    public static SpeechToTextProviderStatus Ready(string providerName, string modelPath, string? message = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        return new SpeechToTextProviderStatus(
            providerName,
            IsAvailable: true,
            IsConfigured: true,
            ModelPath: modelPath,
            Message: message ?? "Local STT provider is ready.");
    }
}
