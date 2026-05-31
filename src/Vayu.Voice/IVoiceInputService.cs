namespace Vayu.Voice;

/// <summary>
/// Captures speech and turns it into text. M4.1 defines the contract;
/// concrete providers (Whisper.cpp / Vosk local, Azure cloud under consent)
/// land in M4.3.
/// </summary>
/// <remarks>
/// Hard rules every implementation inherits:
/// <list type="bullet">
/// <item>No capture without an explicit user action — <see cref="StartPushToTalkAsync"/> is only ever called from a held push-to-talk control.</item>
/// <item>No always-listening. Capture ends on <see cref="StopAsync"/> or when the control is released.</item>
/// <item>No raw audio is logged or persisted. Only the transcript leaves this service.</item>
/// <item>Cloud STT requires explicit consent (the cloud-consent path), gated before any audio leaves the device.</item>
/// </list>
/// </remarks>
public interface IVoiceInputService
{
    /// <summary>Reports microphone availability + permission. Never opens the device.</summary>
    Task<MicrophoneStatus> GetMicrophoneStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins push-to-talk capture for <paramref name="session"/>. Capture runs
    /// until <see cref="StopAsync"/> or cancellation. Returns the recognition
    /// result for the captured audio.
    /// </summary>
    Task<VoiceRecognitionResult> StartPushToTalkAsync(VoiceSession session, CancellationToken cancellationToken = default);

    /// <summary>Stops any in-progress capture. Safe to call when idle.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
