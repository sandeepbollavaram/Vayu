namespace Vayu.Voice;

/// <summary>
/// M4.2 placeholder <see cref="IVoiceInputService"/>. Drives the push-to-talk
/// UI state machine <b>without capturing any audio</b> — real speech
/// recognition lands in M4.3. It opens no microphone, makes no cloud call, and
/// never fakes a transcript.
/// </summary>
/// <remarks>
/// <see cref="StartPushToTalkAsync"/> returns a clearly-labelled "recognition
/// arrives in M4.3" failure rather than a pretend transcript, so the UI can
/// show the Listening → (no result yet) flow honestly.
/// </remarks>
public sealed class StubVoiceInputService : IVoiceInputService
{
    /// <summary>Provider id used in results so logs show this is the stub, not a real engine.</summary>
    public const string ProviderName = "stub";

    /// <inheritdoc />
    public Task<MicrophoneStatus> GetMicrophoneStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new MicrophoneStatus(
            // The stub does not probe hardware; it reports "not yet capturing".
            IsAvailable: true,
            PermissionGranted: false,
            DeviceName: null,
            Message: "Push-to-talk UI is ready. Real transcription arrives in M4.3 — no microphone is captured yet."));

    /// <inheritdoc />
    public Task<VoiceRecognitionResult> StartPushToTalkAsync(VoiceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        // No capture. Honest "not implemented yet" result — never a fake transcript.
        return Task.FromResult(VoiceRecognitionResult.Failed(
            "Real speech recognition arrives in M4.3.", durationMs: 0, providerName: ProviderName));
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
