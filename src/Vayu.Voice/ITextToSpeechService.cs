namespace Vayu.Voice;

/// <summary>
/// Speaks text aloud. M4.1 defines the contract; a System-TTS provider lands
/// in M4.4, with Piper (local) as an optional follow-up.
/// </summary>
/// <remarks>
/// Speaking is opt-in and always interruptible via <see cref="StopAsync"/>.
/// The request carries no secret and no audio.
/// </remarks>
public interface ITextToSpeechService
{
    /// <summary>Speaks <paramref name="request"/>. Returns when playback finishes (or is stopped).</summary>
    Task<SpeechSynthesisResult> SpeakAsync(SpeechSynthesisRequest request, CancellationToken cancellationToken = default);

    /// <summary>Stops any in-progress speech. Safe to call when idle.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
