namespace Vayu.Voice;

/// <summary>
/// Configuration for turning a spoken transcript into a dispatched command.
/// Voice commands are <b>off by default</b>; a transcript is only dispatched
/// when it clears the confidence floor. No secrets.
/// </summary>
public sealed record VoiceCommandOptions
{
    /// <summary>Master switch. Default <see langword="false"/> — push-to-talk transcribes only until the user opts in.</summary>
    public bool EnableVoiceCommands { get; init; }

    /// <summary>Transcript confidence floor (0..1). Below this, nothing is dispatched. Default 0.70.</summary>
    public double MinimumConfidence { get; init; } = 0.70;

    /// <summary>Prefix for the <c>CommandRequest.Source</c>, producing e.g. <c>voice/whisper</c>.</summary>
    public string SourcePrefix { get; init; } = "voice";

    /// <summary>Speak a short result phrase after dispatch, but only when TTS is enabled. Default <see langword="true"/>.</summary>
    public bool SpeakResultWhenTtsEnabled { get; init; } = true;

    /// <summary>Hard cap on transcript length sent to the runtime. Default 500.</summary>
    public int MaxTranscriptChars { get; init; } = 500;
}
