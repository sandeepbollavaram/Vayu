namespace Vayu.Voice;

/// <summary>
/// How a voice interaction is triggered. Only <see cref="PushToTalk"/> is
/// usable in M4.1; the others are placeholders for later milestones and must
/// never enable always-listening capture on their own.
/// </summary>
public enum VoiceInputMode
{
    /// <summary>User holds a key/button to capture. The only active mode in M4.1.</summary>
    PushToTalk = 0,

    /// <summary>Planned: wake-word ("Hey Vayu") trigger. Not implemented; placeholder only.</summary>
    WakeWordPlanned = 1,

    /// <summary>Planned: clap trigger. Not implemented; placeholder only.</summary>
    ClapTriggerPlanned = 2,
}

/// <summary>Helpers describing which voice modes are actually usable today.</summary>
public static class VoiceInputModes
{
    /// <summary>True when <paramref name="mode"/> can be used in the current milestone (M4.1 = push-to-talk only).</summary>
    public static bool IsActive(VoiceInputMode mode) => mode == VoiceInputMode.PushToTalk;
}
