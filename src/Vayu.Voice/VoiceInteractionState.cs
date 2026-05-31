namespace Vayu.Voice;

/// <summary>
/// The phase a voice interaction is in. Drives the Vayu Sphere animation and
/// the activity feed. Provider-neutral — no STT/TTS engine is named here.
/// </summary>
public enum VoiceInteractionState
{
    /// <summary>Not capturing. The default resting state.</summary>
    Idle = 0,

    /// <summary>Microphone is open and capturing (push-to-talk held).</summary>
    Listening = 1,

    /// <summary>Captured audio is being turned into text.</summary>
    Transcribing = 2,

    /// <summary>The transcript is being planned by the AI Router.</summary>
    Thinking = 3,

    /// <summary>The resulting plan is running through the permission/agent pipeline.</summary>
    Executing = 4,

    /// <summary>Vayu is speaking a response (text-to-speech).</summary>
    Speaking = 5,

    /// <summary>Something failed. Carries a redaction-safe message, never audio.</summary>
    Error = 6,

    /// <summary>The user stopped/cancelled the interaction.</summary>
    Cancelled = 7,
}
