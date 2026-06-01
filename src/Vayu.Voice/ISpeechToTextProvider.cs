namespace Vayu.Voice;

/// <summary>
/// A local speech-to-text engine. M4.3 defines the contract and a Whisper.cpp
/// shell; real native transcription is wired progressively. Cloud STT is a
/// separate, consent-gated path — never this one.
/// </summary>
/// <remarks>
/// Hard rules every implementation inherits:
/// <list type="bullet">
/// <item>Audio stays local. The audio bytes are processed in memory and never written to disk or logged.</item>
/// <item>No cloud call. A local provider transcribes on-device.</item>
/// <item>Returns only the transcript text (via <see cref="VoiceRecognitionResult"/>) — never the audio.</item>
/// <item>When no model/runtime is configured, <see cref="GetStatusAsync"/> reports "not configured" and <see cref="TranscribeAsync"/> fails safely (no crash, no fake transcript).</item>
/// </list>
/// </remarks>
public interface ISpeechToTextProvider
{
    /// <summary>Reports provider availability + configuration. Never opens the microphone.</summary>
    Task<SpeechToTextProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes the supplied PCM audio buffer locally. The buffer is held in
    /// memory only and never persisted or logged. Returns the transcript (or a
    /// safe failure when not configured).
    /// </summary>
    Task<VoiceRecognitionResult> TranscribeAsync(ReadOnlyMemory<byte> audio, CancellationToken cancellationToken = default);
}
