namespace Vayu.Voice;

/// <summary>
/// State of the local wake-word ("Hey Vayu") listener. Wake word is opt-in and
/// off by default; it never arms itself.
/// </summary>
public enum WakeWordState
{
    /// <summary>Off. The microphone is not being monitored for the wake word.</summary>
    Disabled = 0,

    /// <summary>Turning on (loading the local model / runtime).</summary>
    Configuring = 1,

    /// <summary>Listening for the wake phrase. A visible indicator must be shown while armed.</summary>
    Armed = 2,

    /// <summary>The wake phrase was just detected; a short cooldown is active before re-arming.</summary>
    Triggered = 3,

    /// <summary>The wake runtime/model is unavailable; not listening. Honest, not fake-ready.</summary>
    Error = 4,
}
