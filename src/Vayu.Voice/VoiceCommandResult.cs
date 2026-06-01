using Vayu.Core;

namespace Vayu.Voice;

/// <summary>
/// Outcome of a push-to-talk voice command. Carries the transcript and the
/// pipeline's <see cref="CommandResult"/> — never raw audio.
/// </summary>
/// <param name="Success">True when the voice flow ran without an internal error (independent of whether it dispatched).</param>
/// <param name="WasDispatched">True when a transcript was actually sent through <c>IAgentRuntime</c>.</param>
/// <param name="Transcript">The recognised text, when any.</param>
/// <param name="Confidence">Recognition confidence, when reported.</param>
/// <param name="CommandResult">The runtime's result, when the command was dispatched.</param>
/// <param name="ErrorMessage">Redaction-safe reason the command did not dispatch (e.g. "low confidence").</param>
/// <param name="CorrelationId">Correlates with the session, command, and audit rows.</param>
/// <param name="ProviderName">The STT provider that produced the transcript.</param>
public sealed record VoiceCommandResult(
    bool Success,
    bool WasDispatched,
    string Transcript,
    double? Confidence,
    CommandResult? CommandResult,
    string? ErrorMessage,
    Guid CorrelationId,
    string ProviderName)
{
    /// <summary>A result for a transcript that was not dispatched (failed/empty/low-confidence/cancelled).</summary>
    public static VoiceCommandResult NotDispatched(
        string reason, string transcript, double? confidence, Guid correlationId, string providerName)
        => new(
            Success: true,
            WasDispatched: false,
            Transcript: transcript,
            Confidence: confidence,
            CommandResult: null,
            ErrorMessage: reason,
            CorrelationId: correlationId,
            ProviderName: providerName);

    /// <summary>A result for a transcript that was dispatched through the runtime.</summary>
    public static VoiceCommandResult Dispatched(
        string transcript, double? confidence, CommandResult commandResult, Guid correlationId, string providerName)
    {
        ArgumentNullException.ThrowIfNull(commandResult);
        return new VoiceCommandResult(
            Success: true,
            WasDispatched: true,
            Transcript: transcript,
            Confidence: confidence,
            CommandResult: commandResult,
            ErrorMessage: null,
            CorrelationId: correlationId,
            ProviderName: providerName);
    }
}
