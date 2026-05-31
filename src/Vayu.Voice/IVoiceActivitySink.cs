namespace Vayu.Voice;

/// <summary>
/// Receives <see cref="VoiceEvent"/>s for display. Implementations later drive
/// the Vayu Sphere voice-state animation (M4.6) and the right-side activity
/// cards (M19). M4.1 defines the contract only.
/// </summary>
/// <remarks>
/// Sinks must treat events as display-only: a <see cref="VoiceEvent"/> carries
/// no raw audio and no secret, and publishing one must never trigger a side
/// effect (no capture, no dispatch).
/// </remarks>
public interface IVoiceActivitySink
{
    /// <summary>Publishes a voice event to the sink. Should not throw on a slow/absent consumer.</summary>
    Task PublishAsync(VoiceEvent voiceEvent, CancellationToken cancellationToken = default);
}
