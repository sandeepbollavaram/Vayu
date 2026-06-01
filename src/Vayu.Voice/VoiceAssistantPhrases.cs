namespace Vayu.Voice;

/// <summary>
/// The short, neutral phrases Vayu may speak. Kept intentionally terse — no
/// long narration, no command echoes, never any user content or secret.
/// </summary>
public static class VoiceAssistantPhrases
{
    public const string Done = "Done.";
    public const string Cancelled = "Cancelled.";
    public const string NeedsConfirmation = "I need confirmation to do that.";
    public const string TranscriptionNotReady = "Voice transcription is not ready yet.";
    public const string OfflineAiNotReady = "Offline AI is not ready yet.";
    public const string Listening = "Listening.";

    /// <summary>All canned phrases, for tests and UI pickers.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Done, Cancelled, NeedsConfirmation, TranscriptionNotReady, OfflineAiNotReady, Listening,
    ];
}
