using Microsoft.Extensions.DependencyInjection;

using Vayu.Core;
using Vayu.Voice;

namespace Vayu_Desktop.Services;

/// <summary>
/// Bridges the wake word to an action: when "Hey Vayu" is detected, it runs one
/// capture → local transcribe → command-dispatch cycle, and reflects the voice
/// state on the desktop sphere overlay. It owns no audio of its own — capture and
/// transcription go through the existing local services, and dispatch goes
/// through the same permission/audit pipeline as a typed command.
/// </summary>
/// <remarks>
/// Honesty/safety: nothing runs unless the user enabled the wake word (which
/// opens the mic) and a real transcript is produced; a failed or empty transcript
/// dispatches nothing. The coordinator never bypasses the runtime.
/// </remarks>
public sealed class WakeWordCoordinator : IDisposable
{
    private readonly IWakeWordService _wake;
    private readonly IServiceProvider _services;
    private int _busy;

    public WakeWordCoordinator(IWakeWordService wake, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(wake);
        ArgumentNullException.ThrowIfNull(services);
        _wake = wake;
        _services = services;
        _wake.Triggered += OnTriggered;
        _wake.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, WakeWordState state)
    {
        var armed = state is WakeWordState.Armed or WakeWordState.Triggered;
        App.SphereOverlay?.SetListeningIndicator(armed);
    }

    private async void OnTriggered(object? sender, WakeWordTriggeredEventArgs e)
    {
        // One cycle at a time — ignore re-triggers while we're already handling one.
        if (Interlocked.Exchange(ref _busy, 1) == 1)
        {
            return;
        }
        try
        {
            await RunOneVoiceCommandAsync().ConfigureAwait(true);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private async Task RunOneVoiceCommandAsync()
    {
        var capture = _services.GetService<IAudioCaptureService>();
        var stt = _services.GetService<ISpeechToTextProvider>();
        var runtime = _services.GetService<IAgentRuntime>();
        if (capture is null || stt is null || runtime is null)
        {
            return;
        }

        App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Listening);

        AudioCaptureResult audio;
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6)))
        {
            try
            {
                audio = await capture.StartCaptureAsync(cts.Token).ConfigureAwait(true);
            }
#pragma warning disable CA1031 // Capture failure → no action, reset sphere.
            catch (Exception)
            {
                App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Idle);
                return;
            }
#pragma warning restore CA1031
        }

        if (!audio.HasAudio)
        {
            App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Idle);
            return;
        }

        App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Transcribing);
        VoiceRecognitionResult transcript;
        try
        {
            transcript = await stt.TranscribeAsync(audio.Pcm16).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Transcription failure → no dispatch.
        catch (Exception)
        {
            App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Idle);
            return;
        }
#pragma warning restore CA1031

        if (!transcript.Success || string.IsNullOrWhiteSpace(transcript.Transcript))
        {
            App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Idle);
            return;
        }

        App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Thinking);
        var request = new CommandRequest
        {
            Text = transcript.Transcript,
            Source = $"voice/{transcript.ProviderName}",
        };
        try
        {
            await runtime.DispatchAsync(request).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Dispatch is already permission/audit-gated; never crash on it.
        catch (Exception)
        {
            // Swallowed — the runtime records its own result/audit.
        }
#pragma warning restore CA1031
        finally
        {
            App.SphereOverlay?.SetVoiceState(VoiceInteractionState.Idle);
        }
    }

    /// <summary>Arms the wake word (opens the mic) if it is enabled + model present.</summary>
    public Task<WakeWordState> StartAsync() => _wake.StartAsync();

    /// <summary>Stops the wake word.</summary>
    public Task StopAsync() => _wake.StopAsync();

    public void Dispose()
    {
        _wake.Triggered -= OnTriggered;
        _wake.StateChanged -= OnStateChanged;
    }
}
