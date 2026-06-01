using Vayu.Core;

namespace Vayu.Voice;

/// <summary>
/// Wires a spoken transcript into the <b>existing</b> command pipeline (M4.5).
/// It opens no new execution path: a recognised transcript becomes a
/// <see cref="CommandRequest"/> with <c>Source = "voice/&lt;provider&gt;"</c>
/// and is dispatched through <see cref="IAgentRuntime"/> exactly like a typed
/// command — so the AI Router, <c>IPermissionService</c>, and the audit log
/// gate it identically.
/// </summary>
/// <remarks>
/// Dispatch happens <b>only</b> when the transcript succeeded, is non-empty, and
/// clears the confidence floor. Failed / empty / low-confidence / cancelled
/// transcripts dispatch nothing. Voice is off by default. After dispatch the
/// service may speak a short, secret-safe phrase (never the transcript or any
/// command text) when TTS is enabled.
/// </remarks>
public sealed class VoiceCommandService : IVoiceCommandService
{
    private readonly IVoiceInputService _voiceInput;
    private readonly IAgentRuntime _runtime;
    private readonly IClock _clock;
    private readonly VoiceCommandOptions _options;
    private readonly IVoiceActivitySink? _sink;
    private readonly ITextToSpeechService? _tts;
    private readonly VoiceTtsState? _ttsState;

    private CancellationTokenSource? _activeCts;

    public VoiceCommandService(
        IVoiceInputService voiceInput,
        IAgentRuntime runtime,
        IClock clock,
        VoiceCommandOptions? options = null,
        IVoiceActivitySink? sink = null,
        ITextToSpeechService? tts = null,
        VoiceTtsState? ttsState = null)
    {
        ArgumentNullException.ThrowIfNull(voiceInput);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(clock);
        _voiceInput = voiceInput;
        _runtime = runtime;
        _clock = clock;
        _options = options ?? new VoiceCommandOptions();
        _sink = sink;
        _tts = tts;
        _ttsState = ttsState;
    }

    /// <inheritdoc />
    public async Task<CommandResult?> StartPushToTalkCommandAsync(CancellationToken cancellationToken = default)
    {
        var detailed = await StartPushToTalkCommandDetailedAsync(cancellationToken).ConfigureAwait(false);
        return detailed.CommandResult;
    }

    /// <summary>
    /// Like <see cref="StartPushToTalkCommandAsync"/> but returns the full
    /// <see cref="VoiceCommandResult"/> (transcript + dispatch status) for the UI.
    /// </summary>
    public async Task<VoiceCommandResult> StartPushToTalkCommandDetailedAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCts = cts;
        try
        {
            var session = VoiceSession.StartPushToTalk(_clock.UtcNow);
            await PublishAsync(session, "Listening.").ConfigureAwait(false);

            var recognition = await _voiceInput.StartPushToTalkAsync(session, cts.Token).ConfigureAwait(false);
            var transcript = (recognition.Transcript ?? string.Empty).Trim();
            var provider = recognition.ProviderName;

            // Gate 1: recognition must have succeeded with a non-empty transcript.
            if (!recognition.Success || transcript.Length == 0)
            {
                await PublishAsync(session.WithState(VoiceInteractionState.Idle), "No transcript.").ConfigureAwait(false);
                return VoiceCommandResult.NotDispatched(
                    recognition.ErrorMessage ?? "No transcript was produced.",
                    transcript, recognition.Confidence, session.CorrelationId, provider);
            }

            // Gate 2: voice commands must be enabled (off by default).
            if (!_options.EnableVoiceCommands)
            {
                return VoiceCommandResult.NotDispatched(
                    "Voice commands are off. Enable them to dispatch a transcript.",
                    transcript, recognition.Confidence, session.CorrelationId, provider);
            }

            // Gate 3: confidence floor.
            if (recognition.Confidence is { } c && c < _options.MinimumConfidence)
            {
                return VoiceCommandResult.NotDispatched(
                    $"Transcript confidence {c:0.00} is below the floor {_options.MinimumConfidence:0.00}.",
                    transcript, recognition.Confidence, session.CorrelationId, provider);
            }

            // Cap length before it enters the runtime.
            if (_options.MaxTranscriptChars > 0 && transcript.Length > _options.MaxTranscriptChars)
            {
                transcript = transcript[.._options.MaxTranscriptChars];
            }

            // Build the CommandRequest — voice is just another source.
            var request = new CommandRequest
            {
                Text = transcript,
                Source = $"{_options.SourcePrefix}/{provider}",
                CreatedAtUtc = _clock.UtcNow,
                CorrelationId = session.CorrelationId,
                Metadata = BuildMetadata(provider, recognition.Confidence),
            };

            await PublishAsync(session.WithState(VoiceInteractionState.Thinking), "Planning.").ConfigureAwait(false);

            // Dispatch through the SAME runtime as typed commands — no bypass.
            var result = await _runtime.DispatchAsync(request, cts.Token).ConfigureAwait(false);

            await PublishAsync(session.WithState(VoiceInteractionState.Executing), "Dispatched.").ConfigureAwait(false);
            await SpeakResultAsync(result, cts.Token).ConfigureAwait(false);

            return VoiceCommandResult.Dispatched(transcript, recognition.Confidence, result, session.CorrelationId, provider);
        }
        finally
        {
            _activeCts = null;
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        _activeCts?.Cancel();
        return _voiceInput.StopAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(string provider, double? confidence)
    {
        var meta = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["voice.provider"] = provider,
        };
        if (confidence is { } c)
        {
            meta["voice.confidence"] = c.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
        return meta;
    }

    /// <summary>Speaks a short, secret-safe phrase for the outcome — never the transcript or command text.</summary>
    private async Task SpeakResultAsync(CommandResult result, CancellationToken ct)
    {
        if (_tts is null || !_options.SpeakResultWhenTtsEnabled)
        {
            return;
        }
        if (_ttsState is not null && !_ttsState.Enabled)
        {
            return;
        }

        var phrase = result.Status switch
        {
            CommandStatus.Success => VoiceAssistantPhrases.Done,
            CommandStatus.PermissionRequired => VoiceAssistantPhrases.NeedsConfirmation,
            CommandStatus.NeedsClarification => VoiceAssistantPhrases.NeedsConfirmation,
            CommandStatus.Cancelled => VoiceAssistantPhrases.Cancelled,
            _ => "I could not complete that.",
        };

        try
        {
            await _tts.SpeakAsync(new SpeechSynthesisRequest(phrase), ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Speaking a result must never fail the command.
        catch
        {
            // Non-fatal: the command already ran; speech is a courtesy.
        }
#pragma warning restore CA1031
    }

    private async Task PublishAsync(VoiceSession session, string message)
    {
        if (_sink is null)
        {
            return;
        }
        await _sink.PublishAsync(VoiceEvent.FromState(session, message, _clock.UtcNow)).ConfigureAwait(false);
    }
}
