namespace Vayu.Voice;

/// <summary>
/// A real local STT provider whose native transcription step is supplied as an
/// injected delegate. This keeps <c>Vayu.Voice</c> free of any audio/native
/// dependency (so CI stays clean) while still running <b>genuine</b>
/// transcription when the desktop app wires a real engine (e.g. Whisper) into
/// <see cref="_transcribe"/>.
/// </summary>
/// <remarks>
/// Honesty rules:
/// <list type="bullet">
/// <item>No cloud call — the delegate is expected to run a local engine.</item>
/// <item>If local STT is off, no model file is configured, or no transcribe
/// delegate is wired, <see cref="GetStatusAsync"/> reports "not configured" and
/// <see cref="TranscribeAsync"/> returns a safe failure — <b>never</b> a
/// fabricated transcript.</item>
/// <item>The audio buffer is passed straight to the delegate and never logged
/// or persisted by this class.</item>
/// </list>
/// </remarks>
public sealed class DelegatingLocalSttProvider : ISpeechToTextProvider
{
    /// <summary>The real, local transcription step. Receives in-memory audio; returns a transcript result.</summary>
    public delegate Task<VoiceRecognitionResult> TranscribeDelegate(
        ReadOnlyMemory<byte> audio,
        string modelPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// A lightweight readiness probe: given a model path, reports whether the
    /// local engine can actually load it (returning a safe message either way).
    /// Lets the desktop confirm genuine readiness without a full transcription —
    /// so the UI never shows "Ready" for a model the engine can't load.
    /// </summary>
    public delegate (bool Ready, string Message) ReadinessProbe(string modelPath);

    private readonly LocalSpeechToTextOptions _options;
    private readonly Func<string, bool> _modelExists;
    private readonly TranscribeDelegate? _transcribe;
    private readonly ReadinessProbe? _readinessProbe;

    /// <param name="options">Local STT options (enable flag + model path + provider name).</param>
    /// <param name="transcribe">
    /// The real local transcription delegate. When <see langword="null"/>, the
    /// provider reports the engine as unavailable and never fabricates a result.
    /// </param>
    /// <param name="modelExists">Model-existence probe; defaults to <see cref="File.Exists"/>.</param>
    /// <param name="readinessProbe">
    /// Optional load-readiness probe. When provided, the provider reports
    /// "Ready" only if this confirms the engine can load the model.
    /// </param>
    public DelegatingLocalSttProvider(
        LocalSpeechToTextOptions? options = null,
        TranscribeDelegate? transcribe = null,
        Func<string, bool>? modelExists = null,
        ReadinessProbe? readinessProbe = null)
    {
        _options = options ?? new LocalSpeechToTextOptions();
        _transcribe = transcribe;
        _modelExists = modelExists ?? File.Exists;
        _readinessProbe = readinessProbe;
    }

    private string ProviderId => _options.PreferredProvider;

    /// <inheritdoc />
    public Task<SpeechToTextProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableLocalStt)
        {
            return Task.FromResult(SpeechToTextProviderStatus.NotConfigured(
                ProviderId, "Local STT is off. Enable it and configure a model to transcribe."));
        }

        var modelPath = _options.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath) || !_modelExists(modelPath))
        {
            return Task.FromResult(SpeechToTextProviderStatus.NotConfigured(
                ProviderId, "No local STT model is configured. Set a model path in Settings → Voice Setup."));
        }

        if (_transcribe is null)
        {
            // Model is present but no local engine is wired on this machine.
            return Task.FromResult(new SpeechToTextProviderStatus(
                ProviderId,
                IsAvailable: false,
                IsConfigured: false,
                ModelPath: modelPath,
                Message: "Model found, but the local STT runtime is not available in this build. Transcription is unavailable."));
        }

        // Confirm the engine can actually LOAD the model before claiming Ready —
        // a present file is not enough (could be the wrong format or corrupt).
        if (_readinessProbe is not null)
        {
            var (ready, message) = _readinessProbe(modelPath);
            if (!ready)
            {
                return Task.FromResult(new SpeechToTextProviderStatus(
                    ProviderId,
                    IsAvailable: false,
                    IsConfigured: false,
                    ModelPath: modelPath,
                    Message: message));
            }
            return Task.FromResult(SpeechToTextProviderStatus.Ready(ProviderId, modelPath, message));
        }

        return Task.FromResult(SpeechToTextProviderStatus.Ready(
            ProviderId, modelPath, "Local STT is ready to transcribe on this device."));
    }

    /// <inheritdoc />
    public async Task<VoiceRecognitionResult> TranscribeAsync(ReadOnlyMemory<byte> audio, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsConfigured || _transcribe is null || string.IsNullOrWhiteSpace(_options.ModelPath))
        {
            return VoiceRecognitionResult.Failed(status.Message, durationMs: 0, providerName: ProviderId);
        }

        if (audio.IsEmpty)
        {
            return VoiceRecognitionResult.Failed(
                "No audio was captured to transcribe.", durationMs: 0, providerName: ProviderId);
        }

        // Run the REAL local engine. Never fabricate a transcript.
        return await _transcribe(audio, _options.ModelPath, cancellationToken).ConfigureAwait(false);
    }
}
