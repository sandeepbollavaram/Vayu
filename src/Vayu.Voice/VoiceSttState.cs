namespace Vayu.Voice;

/// <summary>
/// Process-level local-STT configuration the Settings → Voice Setup surface
/// updates at runtime. Holds the current <see cref="LocalSpeechToTextOptions"/>
/// (enable flag + model path) — no audio, no secrets.
/// </summary>
/// <remarks>
/// Default is local STT <b>off</b> with no model path. Updating this does not
/// start any capture; it only changes what the STT provider reports and uses on
/// the next push-to-talk.
/// </remarks>
public sealed class VoiceSttState
{
    private readonly object _lock = new();
    private LocalSpeechToTextOptions _options;

    public VoiceSttState(LocalSpeechToTextOptions? initial = null)
    {
        _options = initial ?? new LocalSpeechToTextOptions();
    }

    /// <summary>The current options snapshot.</summary>
    public LocalSpeechToTextOptions Options
    {
        get { lock (_lock) { return _options; } }
    }

    /// <summary>Enables local STT and sets the model path (validated by the caller).</summary>
    public void Configure(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        lock (_lock)
        {
            _options = _options with { EnableLocalStt = true, ModelPath = modelPath };
        }
    }

    /// <summary>Turns local STT off and clears the model path.</summary>
    public void Disable()
    {
        lock (_lock)
        {
            _options = _options with { EnableLocalStt = false, ModelPath = null };
        }
    }
}
