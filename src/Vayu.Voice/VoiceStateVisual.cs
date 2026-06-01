namespace Vayu.Voice;

/// <summary>
/// The visual treatment the Vayu Sphere applies for a voice state. A pure
/// token (no WinUI) so the mapping is unit-testable; the desktop sphere turns
/// each token into a storyboard + accent colour.
/// </summary>
public enum VoiceVisualToken
{
    /// <summary>Calm breathing pulse (the resting visual).</summary>
    IdlePulse = 0,

    /// <summary>Brighter, faster cyan pulse while capturing.</summary>
    ListeningPulse = 1,

    /// <summary>Tight blue scanning/processing feel while turning audio into text.</summary>
    TranscribingScan = 2,

    /// <summary>Violet/blue processing accent while planning.</summary>
    ThinkingAccent = 3,

    /// <summary>Strong teal processing ring while the plan runs.</summary>
    ExecutingRing = 4,

    /// <summary>Soft outward glow while speaking a response.</summary>
    SpeakingGlow = 5,

    /// <summary>Red/amber error flash.</summary>
    ErrorFlash = 6,

    /// <summary>Amber fade, then back to idle.</summary>
    CancelledFade = 7,
}

/// <summary>
/// Maps a <see cref="VoiceInteractionState"/> onto the sphere's
/// <see cref="VoiceVisualToken"/>. Pure and deterministic; the only place the
/// state→visual decision lives, so both the sphere and tests agree.
/// </summary>
public static class VoiceStateVisualMapper
{
    /// <summary>Returns the visual token for <paramref name="state"/>; unknown values map to <see cref="VoiceVisualToken.IdlePulse"/>.</summary>
    public static VoiceVisualToken Map(VoiceInteractionState state) => state switch
    {
        VoiceInteractionState.Listening => VoiceVisualToken.ListeningPulse,
        VoiceInteractionState.Transcribing => VoiceVisualToken.TranscribingScan,
        VoiceInteractionState.Thinking => VoiceVisualToken.ThinkingAccent,
        VoiceInteractionState.Executing => VoiceVisualToken.ExecutingRing,
        VoiceInteractionState.Speaking => VoiceVisualToken.SpeakingGlow,
        VoiceInteractionState.Error => VoiceVisualToken.ErrorFlash,
        VoiceInteractionState.Cancelled => VoiceVisualToken.CancelledFade,
        _ => VoiceVisualToken.IdlePulse, // Idle and any unknown
    };

    /// <summary>Short uppercase chip label for the Home Voice card.</summary>
    public static string ChipLabel(VoiceInteractionState state) => state switch
    {
        VoiceInteractionState.Listening => "LISTENING",
        VoiceInteractionState.Transcribing => "TRANSCRIBING",
        VoiceInteractionState.Thinking => "THINKING",
        VoiceInteractionState.Executing => "EXECUTING",
        VoiceInteractionState.Speaking => "SPEAKING",
        VoiceInteractionState.Error => "ERROR",
        VoiceInteractionState.Cancelled => "CANCELLED",
        _ => "IDLE",
    };
}
