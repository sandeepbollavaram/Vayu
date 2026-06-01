namespace Vayu.Voice;

/// <summary>
/// Whisper.cpp local STT provider <b>shell</b> (M4.3). It is provider-ready —
/// it answers the <see cref="ISpeechToTextProvider"/> contract and detects
/// whether a model is configured — but it deliberately ships <b>no native
/// binary</b>, so CI and the build stay clean. Native transcription is wired
/// progressively once a model/runtime is configured.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>No cloud call, ever — Whisper.cpp is fully local.</item>
/// <item>If <see cref="LocalSpeechToTextOptions.ModelPath"/> is missing or the file is absent, <see cref="GetStatusAsync"/> reports "not configured" and <see cref="TranscribeAsync"/> returns a safe failure.</item>
/// <item>Even with a model path present, M4.3 returns a clearly-labelled "shell ready; native runtime setup required" failure rather than a fabricated transcript.</item>
/// <item>The audio buffer is never written to disk or logged.</item>
/// </list>
/// </remarks>
public sealed class WhisperCppSpeechToTextProvider : ISpeechToTextProvider
{
    /// <summary>Stable provider id.</summary>
    public const string ProviderId = "whispercpp";

    private readonly LocalSpeechToTextOptions _options;
    private readonly Func<string, bool> _modelExists;

    /// <summary>Standard constructor — checks the real filesystem for the model.</summary>
    public WhisperCppSpeechToTextProvider(LocalSpeechToTextOptions? options = null)
        : this(options, File.Exists)
    {
    }

    /// <summary>Test-friendly constructor accepting a model-existence probe so tests never touch disk.</summary>
    public WhisperCppSpeechToTextProvider(LocalSpeechToTextOptions? options, Func<string, bool> modelExists)
    {
        ArgumentNullException.ThrowIfNull(modelExists);
        _options = options ?? new LocalSpeechToTextOptions();
        _modelExists = modelExists;
    }

    /// <inheritdoc />
    public Task<SpeechToTextProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.EnableLocalStt)
        {
            return Task.FromResult(SpeechToTextProviderStatus.NotConfigured(
                ProviderId, "Local STT is off. Enable it and configure a Whisper.cpp model to transcribe."));
        }

        var modelPath = _options.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath) || !_modelExists(modelPath))
        {
            return Task.FromResult(SpeechToTextProviderStatus.NotConfigured(
                ProviderId, "No Whisper.cpp model is configured. Set a model path in voice settings."));
        }

        // Model is present but native transcription is not wired in M4.3.
        return Task.FromResult(new SpeechToTextProviderStatus(
            ProviderId,
            IsAvailable: true,
            IsConfigured: true,
            ModelPath: modelPath,
            Message: "Whisper.cpp model detected. Native transcription runtime setup arrives progressively."));
    }

    /// <inheritdoc />
    public async Task<VoiceRecognitionResult> TranscribeAsync(ReadOnlyMemory<byte> audio, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!status.IsConfigured)
        {
            return VoiceRecognitionResult.Failed(status.Message, durationMs: 0, providerName: ProviderId);
        }

        // M4.3 ships the shell only — no native runtime. Honest failure, never a fake transcript.
        // (The audio buffer is not persisted or logged here.)
        return VoiceRecognitionResult.Failed(
            "Whisper.cpp provider shell is ready; native transcription integration requires model/runtime setup.",
            durationMs: 0,
            providerName: ProviderId);
    }
}
