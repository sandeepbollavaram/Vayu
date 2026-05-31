namespace Vayu.Voice;

/// <summary>
/// A single voice interaction from trigger to completion. Carries the
/// transcript text (never raw audio) and correlates with the downstream
/// <c>CommandRequest</c> / audit rows.
/// </summary>
/// <param name="SessionId">Unique id for this voice session.</param>
/// <param name="StartedAtUtc">When capture started.</param>
/// <param name="State">Current interaction state.</param>
/// <param name="InputMode">How the session was triggered (push-to-talk in M4.1).</param>
/// <param name="EndedAtUtc">When the session finished; null while in flight.</param>
/// <param name="Transcript">The recognised text, if any. <b>No raw audio is ever stored.</b></param>
/// <param name="ErrorMessage">Redaction-safe failure reason, if the session errored.</param>
/// <param name="CorrelationId">Correlates with the resulting command + audit trail.</param>
public sealed record VoiceSession(
    Guid SessionId,
    DateTimeOffset StartedAtUtc,
    VoiceInteractionState State,
    VoiceInputMode InputMode,
    DateTimeOffset? EndedAtUtc = null,
    string? Transcript = null,
    string? ErrorMessage = null,
    Guid CorrelationId = default)
{
    /// <summary>Starts a fresh idle→listening session for push-to-talk.</summary>
    public static VoiceSession StartPushToTalk(DateTimeOffset startedAtUtc, Guid? correlationId = null)
        => new(
            SessionId: Guid.NewGuid(),
            StartedAtUtc: startedAtUtc,
            State: VoiceInteractionState.Listening,
            InputMode: VoiceInputMode.PushToTalk,
            CorrelationId: correlationId ?? Guid.NewGuid());

    /// <summary>Returns a copy advanced to <paramref name="state"/>.</summary>
    public VoiceSession WithState(VoiceInteractionState state) => this with { State = state };

    /// <summary>Returns a copy marked cancelled and ended.</summary>
    public VoiceSession Cancelled(DateTimeOffset endedAtUtc)
        => this with { State = VoiceInteractionState.Cancelled, EndedAtUtc = endedAtUtc };

    /// <summary>Returns a copy marked errored with a redaction-safe message.</summary>
    public VoiceSession Errored(string errorMessage, DateTimeOffset endedAtUtc)
        => this with { State = VoiceInteractionState.Error, ErrorMessage = errorMessage, EndedAtUtc = endedAtUtc };
}
