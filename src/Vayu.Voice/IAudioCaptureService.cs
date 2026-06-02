namespace Vayu.Voice;

/// <summary>
/// Captures microphone audio for a single push-to-talk interaction. The capture
/// is provider-neutral: the concrete Windows implementation lives in the desktop
/// app so this library stays free of audio-stack and native dependencies.
/// </summary>
/// <remarks>
/// Hard rules every implementation inherits:
/// <list type="bullet">
/// <item><b>Push-to-talk only.</b> Capture begins solely on an explicit
/// <see cref="StartCaptureAsync"/> call (the user pressing Push to talk). There
/// is no always-listening, no wake word, and no clap trigger here.</item>
/// <item><b>Stop stops.</b> Cancelling the supplied token (Stop/Cancel) ends the
/// capture promptly and returns <see cref="AudioCaptureStatus.Cancelled"/>.</item>
/// <item><b>Bounded.</b> Capture never runs longer than
/// <see cref="LocalSpeechToTextOptions.MaxCaptureSeconds"/>.</item>
/// <item><b>Local + in-memory.</b> Audio is returned as in-memory PCM and is
/// never uploaded, never written to disk (beyond a transient, immediately-deleted
/// temp file if a runtime strictly requires one), and never logged.</item>
/// </list>
/// </remarks>
public interface IAudioCaptureService
{
    /// <summary>
    /// Reports whether a capture device is available, without opening it.
    /// </summary>
    Task<MicrophoneStatus> GetMicrophoneStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Captures from the microphone until the caller cancels
    /// <paramref name="cancellationToken"/> (Stop/Cancel) or the configured
    /// maximum duration elapses, then returns the captured audio in memory.
    /// Never starts on its own; only an explicit call begins capture.
    /// </summary>
    Task<AudioCaptureResult> StartCaptureAsync(CancellationToken cancellationToken = default);
}
