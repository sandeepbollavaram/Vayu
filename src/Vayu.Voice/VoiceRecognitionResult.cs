namespace Vayu.Voice;

/// <summary>
/// Outcome of a speech-to-text attempt. Carries the transcript text and a
/// confidence — never the raw audio.
/// </summary>
/// <param name="Success">True when a usable transcript was produced.</param>
/// <param name="Transcript">The recognised text. Empty on failure.</param>
/// <param name="Confidence">0..1 recognition confidence, when the provider reports one.</param>
/// <param name="ErrorMessage">Redaction-safe failure reason; null on success.</param>
/// <param name="DurationMs">How long recognition took, in milliseconds.</param>
/// <param name="ProviderName">Provider id, e.g. <c>"whisper-local"</c> (none in M4.1).</param>
public sealed record VoiceRecognitionResult(
    bool Success,
    string Transcript,
    double? Confidence,
    string? ErrorMessage,
    long DurationMs,
    string ProviderName)
{
    /// <summary>Builds a success result. Requires a non-empty transcript.</summary>
    public static VoiceRecognitionResult Succeeded(string transcript, double? confidence, long durationMs, string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return new VoiceRecognitionResult(true, transcript, confidence, null, durationMs, providerName);
    }

    /// <summary>Builds a failure result with a redaction-safe reason and empty transcript.</summary>
    public static VoiceRecognitionResult Failed(string errorMessage, long durationMs, string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return new VoiceRecognitionResult(false, string.Empty, null, errorMessage, durationMs, providerName);
    }
}
