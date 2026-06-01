namespace Vayu.Voice;

/// <summary>
/// Process-level, user-flippable opt-in for spoken responses. The Settings
/// toggle writes <see cref="Enabled"/>; <c>SystemTextToSpeechService</c> reads
/// it so TTS can be turned on/off without a restart. Defaults to OFF.
/// </summary>
public sealed class VoiceTtsState
{
    private volatile bool _enabled;

    public VoiceTtsState(bool initiallyEnabled = false) => _enabled = initiallyEnabled;

    /// <summary>True when the user has opted into spoken responses.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }
}
