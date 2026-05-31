namespace Vayu.Voice;

/// <summary>
/// A point-in-time voice interaction event for the activity feed and the Vayu
/// Sphere. Carries a state + a redaction-safe message — never raw audio.
/// </summary>
/// <param name="TimestampUtc">When the event occurred.</param>
/// <param name="State">The interaction state at this moment.</param>
/// <param name="Message">Short, redaction-safe description for the UI.</param>
/// <param name="CorrelationId">Correlates with the session + downstream command.</param>
/// <param name="IsError">True when this event represents a failure.</param>
public sealed record VoiceEvent(
    DateTimeOffset TimestampUtc,
    VoiceInteractionState State,
    string Message,
    Guid CorrelationId,
    bool IsError = false)
{
    /// <summary>Builds an event from a session's current state.</summary>
    public static VoiceEvent FromState(VoiceSession session, string message, DateTimeOffset timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new VoiceEvent(
            TimestampUtc: timestampUtc,
            State: session.State,
            Message: message,
            CorrelationId: session.CorrelationId,
            IsError: session.State == VoiceInteractionState.Error);
    }
}
