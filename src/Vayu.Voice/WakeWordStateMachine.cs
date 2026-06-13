namespace Vayu.Voice;

/// <summary>
/// Pure, testable state machine for the local wake word. It decides transitions
/// (Disabled → Configuring → Armed → Triggered → Armed) and whether a recognised
/// phrase counts as the wake word — but performs no audio capture itself. The
/// desktop adapter feeds it recognised text from the local engine and acts on
/// the transitions.
/// </summary>
/// <remarks>
/// Honesty rules baked in: it never reaches <see cref="WakeWordState.Armed"/>
/// unless wake word is enabled <em>and</em> a model is present; a missing model
/// yields <see cref="WakeWordState.Error"/> with a clear message — never a fake
/// "ready". Detection is real: only an actual phrase match triggers.
/// </remarks>
public sealed class WakeWordStateMachine
{
    private readonly WakeWordOptions _options;
    private readonly Func<string, bool> _modelExists;

    public WakeWordStateMachine(WakeWordOptions options, Func<string, bool>? modelExists = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _modelExists = modelExists ?? Directory.Exists;
        State = WakeWordState.Disabled;
    }

    /// <summary>The current state.</summary>
    public WakeWordState State { get; private set; }

    /// <summary>A short, redaction-safe explanation for the UI.</summary>
    public string Message { get; private set; } = "Wake word is off.";

    /// <summary>
    /// Requests arming. Returns the resulting state: <see cref="WakeWordState.Armed"/>
    /// when enabled and the model exists, otherwise <see cref="WakeWordState.Disabled"/>
    /// (not enabled) or <see cref="WakeWordState.Error"/> (model missing).
    /// </summary>
    public WakeWordState Arm()
    {
        if (!_options.EnableWakeWord)
        {
            State = WakeWordState.Disabled;
            Message = "Wake word is off. Enable it in Voice settings to use “Hey Vayu”.";
            return State;
        }

        var modelPath = _options.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath) || !_modelExists(modelPath))
        {
            State = WakeWordState.Error;
            Message = "Wake word runtime not configured. Set a local wake/STT model path in Voice settings.";
            return State;
        }

        State = WakeWordState.Armed;
        Message = "Listening for “Hey Vayu”.";
        return State;
    }

    /// <summary>Turns the wake word off.</summary>
    public WakeWordState Disable()
    {
        State = WakeWordState.Disabled;
        Message = "Wake word is off.";
        return State;
    }

    /// <summary>
    /// Feeds a chunk of recognised text from the local engine. Returns
    /// <see langword="true"/> only when armed AND the text contains a wake phrase
    /// — in which case the machine moves to <see cref="WakeWordState.Triggered"/>.
    /// </summary>
    public bool OnRecognized(string? recognizedText)
    {
        if (State != WakeWordState.Armed || string.IsNullOrWhiteSpace(recognizedText))
        {
            return false;
        }
        if (!ContainsWakePhrase(recognizedText, _options))
        {
            return false;
        }
        State = WakeWordState.Triggered;
        Message = "Heard “Hey Vayu”.";
        return true;
    }

    /// <summary>Re-arms after the post-trigger cooldown (caller waits the cooldown).</summary>
    public WakeWordState ReArm()
    {
        if (State == WakeWordState.Triggered)
        {
            State = WakeWordState.Armed;
            Message = "Listening for “Hey Vayu”.";
        }
        return State;
    }

    /// <summary>True when <paramref name="text"/> contains one of the configured wake phrases.</summary>
    public static bool ContainsWakePhrase(string? text, WakeWordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var normalized = text.Trim().ToLowerInvariant();
        foreach (var phrase in options.Phrases)
        {
            if (!string.IsNullOrWhiteSpace(phrase) &&
                normalized.Contains(phrase.ToLowerInvariant(), StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
