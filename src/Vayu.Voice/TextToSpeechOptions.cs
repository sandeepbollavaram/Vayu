namespace Vayu.Voice;

/// <summary>
/// Configuration for spoken responses. Text-to-speech is <b>off by default</b>
/// and opt-in. Holds no secret and no audio.
/// </summary>
public sealed record TextToSpeechOptions
{
    /// <summary>Master switch. Default <see langword="false"/> — Vayu does not speak unless the user enables it.</summary>
    public bool EnableTextToSpeech { get; init; }

    /// <summary>Provider id, e.g. <c>"system"</c> (Windows System TTS).</summary>
    public string ProviderName { get; init; } = "system";

    /// <summary>Optional named system voice. Null = the OS default voice.</summary>
    public string? VoiceName { get; init; }

    /// <summary>Speaking rate multiplier (1.0 = normal).</summary>
    public double Rate { get; init; } = 1.0;

    /// <summary>Playback volume (0..1).</summary>
    public double Volume { get; init; } = 1.0;

    /// <summary>Hard cap on a single utterance. Long text is truncated so Vayu stays terse. Default 300.</summary>
    public int MaxCharsPerUtterance { get; init; } = 300;
}
