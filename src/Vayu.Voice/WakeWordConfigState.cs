namespace Vayu.Voice;

/// <summary>
/// Process-level wake-word configuration the Settings → Voice Setup surface
/// updates at runtime. Holds the current <see cref="WakeWordOptions"/> (enable
/// flag + model path) so enabling the wake word or installing a model takes
/// effect on the next arm without restarting Vayu. Holds no audio and no secret.
/// </summary>
/// <remarks>
/// Default is wake word <b>off</b> with the configured model path. Updating this
/// does not start any capture; it only changes what the wake-word engine reads
/// the next time it is armed. Enabling is the only thing that lets the mic open
/// for monitoring, and it still requires a present model to actually arm.
/// </remarks>
public sealed class WakeWordConfigState
{
    private readonly object _lock = new();
    private WakeWordOptions _options;

    public WakeWordConfigState(WakeWordOptions? initial = null)
    {
        _options = initial ?? new WakeWordOptions();
    }

    /// <summary>The current options snapshot.</summary>
    public WakeWordOptions Options
    {
        get { lock (_lock) { return _options; } }
    }

    /// <summary>Turns the wake word on, keeping the configured model path.</summary>
    public void Enable()
    {
        lock (_lock)
        {
            _options = _options with { EnableWakeWord = true };
        }
    }

    /// <summary>Turns the wake word off.</summary>
    public void Disable()
    {
        lock (_lock)
        {
            _options = _options with { EnableWakeWord = false };
        }
    }

    /// <summary>Sets the local wake/STT model directory (validated by the caller).</summary>
    public void SetModelPath(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        lock (_lock)
        {
            _options = _options with { ModelPath = modelPath };
        }
    }
}
