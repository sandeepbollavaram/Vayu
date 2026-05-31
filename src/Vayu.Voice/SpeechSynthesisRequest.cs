namespace Vayu.Voice;

/// <summary>
/// A request to speak some text aloud. Contains only display text and voice
/// preferences — no secrets, no audio.
/// </summary>
/// <param name="Text">The text to speak.</param>
/// <param name="VoiceName">Optional named system/provider voice. Null = default.</param>
/// <param name="Rate">Optional speaking rate multiplier (1.0 = normal). Null = default.</param>
/// <param name="CorrelationId">Correlates with the originating interaction.</param>
public sealed record SpeechSynthesisRequest(
    string Text,
    string? VoiceName = null,
    double? Rate = null,
    Guid CorrelationId = default);
