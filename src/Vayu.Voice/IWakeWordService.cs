namespace Vayu.Voice;

/// <summary>
/// Fired when the local wake word ("Hey Vayu") is detected. Carries no audio.
/// </summary>
public sealed class WakeWordTriggeredEventArgs : EventArgs
{
    /// <summary>The phrase text the engine recognised (e.g. "hey vayu").</summary>
    public required string RecognizedPhrase { get; init; }
}

/// <summary>
/// Local, on-device wake-word listener. The concrete Windows implementation
/// (Vosk) lives in the desktop app so this library stays free of native audio
/// dependencies. Opt-in and off by default; never arms itself.
/// </summary>
/// <remarks>
/// Hard rules every implementation inherits:
/// <list type="bullet">
/// <item>On-device only — no cloud wake detection.</item>
/// <item>Mic monitoring starts only after <see cref="StartAsync"/>, which the
/// user invokes by enabling the wake word; a visible indicator must show while
/// armed.</item>
/// <item>No raw audio is logged or persisted.</item>
/// <item>If the model/runtime is missing, the state is
/// <see cref="WakeWordState.Error"/> with an honest message — never a fake
/// "ready" and never a fabricated trigger.</item>
/// </list>
/// </remarks>
public interface IWakeWordService
{
    /// <summary>The current wake-word state.</summary>
    WakeWordState State { get; }

    /// <summary>A short, redaction-safe status message for the UI.</summary>
    string StatusMessage { get; }

    /// <summary>Raised on a real wake-phrase detection. The desktop then opens an STT session.</summary>
    event EventHandler<WakeWordTriggeredEventArgs>? Triggered;

    /// <summary>Raised whenever <see cref="State"/> changes (for the UI/indicator).</summary>
    event EventHandler<WakeWordState>? StateChanged;

    /// <summary>
    /// Arms the wake word if enabled and the model is present; otherwise resolves
    /// to <see cref="WakeWordState.Disabled"/> or <see cref="WakeWordState.Error"/>.
    /// </summary>
    Task<WakeWordState> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops listening and returns to <see cref="WakeWordState.Disabled"/>.</summary>
    Task StopAsync();
}
