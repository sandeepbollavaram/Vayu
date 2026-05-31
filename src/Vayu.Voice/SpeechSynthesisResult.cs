namespace Vayu.Voice;

/// <summary>
/// Outcome of a text-to-speech request. No secrets, no audio bytes.
/// </summary>
/// <param name="Success">True when speech playback completed (or started, per provider semantics).</param>
/// <param name="ErrorMessage">Redaction-safe failure reason; null on success.</param>
/// <param name="ProviderName">Provider id, e.g. <c>"system-tts"</c> (none in M4.1).</param>
/// <param name="DurationMs">How long synthesis/playback took, in milliseconds.</param>
public sealed record SpeechSynthesisResult(
    bool Success,
    string? ErrorMessage,
    string ProviderName,
    long DurationMs)
{
    public static SpeechSynthesisResult Succeeded(string providerName, long durationMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return new SpeechSynthesisResult(true, null, providerName, durationMs);
    }

    public static SpeechSynthesisResult Failed(string errorMessage, string providerName, long durationMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return new SpeechSynthesisResult(false, errorMessage, providerName, durationMs);
    }
}
