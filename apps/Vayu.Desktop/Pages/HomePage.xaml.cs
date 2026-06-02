using System.Collections.ObjectModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Vayu.Core;
using Vayu.Memory;
using Vayu.Voice;
using Vayu_Desktop.Controls;

namespace Vayu_Desktop.Pages;

/// <summary>
/// Home dashboard: AI sphere, command console, status chips, quick cards,
/// and a compact recent-activity preview from the audit log.
/// </summary>
public sealed partial class HomePage : Page
{
    private readonly IAgentRuntime _runtime;
    private readonly IAuditLogService _audit;

    // M4.2/M4.3/M4.4/M4.5: push-to-talk UI + local STT + TTS + voice command pipeline.
    private readonly IVoiceInputService? _voiceInput;
    private readonly IVoiceActivitySink? _voiceSink;
    private readonly ISpeechToTextProvider? _sttProvider;
    private readonly IAudioCaptureService? _audioCapture;
    private readonly ITextToSpeechService? _tts;
    private readonly VoiceCommandService? _voiceCommands;
    private bool _voiceCommandsEnabled;
    private VoiceSession? _voiceSession;

    // Real push-to-talk capture (M4.9). The CTS is cancelled by Stop; the task
    // resolves with the in-memory audio so the Stop handler can transcribe it.
    private CancellationTokenSource? _captureCts;
    private Task<AudioCaptureResult>? _captureTask;

    /// <summary>Bound to the "Recent activity" preview at the bottom of the page.</summary>
    public ObservableCollection<string> RecentRows { get; } = new();

    public HomePage()
    {
        InitializeComponent();
        _runtime = App.Services.GetRequiredService<IAgentRuntime>();
        _audit = App.Services.GetRequiredService<IAuditLogService>();
        _voiceInput = App.Services.GetService<IVoiceInputService>();
        _voiceSink = App.Services.GetService<IVoiceActivitySink>();
        _sttProvider = App.Services.GetService<ISpeechToTextProvider>();
        _audioCapture = App.Services.GetService<IAudioCaptureService>();
        _tts = App.Services.GetService<ITextToSpeechService>();
        _voiceCommands = App.Services.GetService<VoiceCommandService>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await RefreshRecentAsync().ConfigureAwait(true);
        await RefreshMicStatusAsync().ConfigureAwait(true);
        await RefreshSttStatusAsync().ConfigureAwait(true);
        await RefreshTtsStatusAsync().ConfigureAwait(true);
    }

    private async Task RefreshTtsStatusAsync()
    {
        if (_tts is null)
        {
            VoiceTtsStatusText.Text = "Text-to-speech: no provider registered.";
            return;
        }
        if (_tts is Vayu.Voice.SystemTextToSpeechService systemTts)
        {
            var status = await systemTts.GetStatusAsync().ConfigureAwait(true);
            VoiceTtsStatusText.Text = $"Text-to-speech ({status.ProviderName}): {status.Message}";
        }
        else
        {
            VoiceTtsStatusText.Text = "Text-to-speech: ready.";
        }
    }

    private async void OnSpeakStatusClick(object sender, RoutedEventArgs e)
    {
        if (_tts is null)
        {
            return;
        }
        // Speak a short, neutral, secret-free phrase — never user content.
        var phrase = Vayu.Voice.VoiceAssistantPhrases.Done;
        var result = await _tts.SpeakAsync(new Vayu.Voice.SpeechSynthesisRequest(phrase)).ConfigureAwait(true);
        if (!result.Success)
        {
            VoiceTtsStatusText.Text = $"Text-to-speech: {result.ErrorMessage}";
        }
    }

    private async void OnStopSpeakingClick(object sender, RoutedEventArgs e)
    {
        if (_tts is not null)
        {
            await _tts.StopAsync().ConfigureAwait(true);
        }
    }

    // ---- M4.2/M4.3: push-to-talk + local STT foundation ----

    private async Task RefreshMicStatusAsync()
    {
        if (_voiceInput is null)
        {
            VoiceMicStatusText.Text = "Voice service unavailable.";
            PushToTalkButton.IsEnabled = false;
            return;
        }
        try
        {
            var status = await _voiceInput.GetMicrophoneStatusAsync().ConfigureAwait(true);
            VoiceMicStatusText.Text = $"Microphone status: {status.Message}";
        }
        catch
        {
            VoiceMicStatusText.Text = "Microphone status: unavailable.";
        }
    }

    private async Task RefreshSttStatusAsync()
    {
        if (_sttProvider is null)
        {
            VoiceSttStatusText.Text = "Local STT: no provider registered.";
            return;
        }
        try
        {
            var status = await _sttProvider.GetStatusAsync().ConfigureAwait(true);
            VoiceSttStatusText.Text = $"Local STT ({status.ProviderName}): {status.Message}";
        }
        catch
        {
            VoiceSttStatusText.Text = "Local STT: status unavailable.";
        }
    }

    private void OnPushToTalkClick(object sender, RoutedEventArgs e)
    {
        if (_audioCapture is null || _captureTask is not null)
        {
            return;
        }

        _voiceSession = VoiceSession.StartPushToTalk(DateTimeOffset.UtcNow);
        SetVoiceUi(VoiceInteractionState.Listening, "LISTENING");
        VoiceTranscriptText.Text = "Listening… press Stop to transcribe.";
        VoiceCommandResultText.Text = string.Empty;
        _ = PublishVoiceAsync("Listening.");

        // Start REAL microphone capture. It runs until Stop cancels the token or
        // the configured max-duration cap elapses. No audio is captured until now.
        _captureCts = new CancellationTokenSource();
        _captureTask = _audioCapture.StartCaptureAsync(_captureCts.Token);
    }

    private void OnVoiceCommandsToggled(object sender, RoutedEventArgs e)
        => _voiceCommandsEnabled = VoiceCommandsToggle.IsOn && _voiceCommands is not null;

    private async void OnStopVoiceClick(object sender, RoutedEventArgs e)
    {
        var captureTask = _captureTask;
        if (captureTask is null)
        {
            return;
        }

        // Stop capture and collect the in-memory audio.
        _captureCts?.Cancel();
        SetVoiceUi(VoiceInteractionState.Transcribing, "TRANSCRIBING");
        await PublishVoiceAsync("Transcribing.").ConfigureAwait(true);

        AudioCaptureResult capture;
        try
        {
            capture = await captureTask.ConfigureAwait(true);
        }
#pragma warning disable CA1031 // UI boundary: surface capture failure, never crash.
        catch (Exception)
        {
            capture = AudioCaptureResult.Failed("Microphone capture failed.");
        }
#pragma warning restore CA1031
        finally
        {
            _captureCts?.Dispose();
            _captureCts = null;
            _captureTask = null;
        }

        if (!capture.HasAudio)
        {
            VoiceTranscriptText.Text = capture.Status == AudioCaptureStatus.Failed
                ? capture.ErrorMessage ?? "Microphone capture failed."
                : "No audio captured.";
            VoiceCommandResultText.Text = string.Empty;
            await ResetVoiceIdleAsync().ConfigureAwait(true);
            return;
        }

        // Transcribe the real captured audio locally (no cloud).
        await TranscribeAndMaybeDispatchAsync(capture.Pcm16).ConfigureAwait(true);
        await ResetVoiceIdleAsync().ConfigureAwait(true);
    }

    private async Task TranscribeAndMaybeDispatchAsync(ReadOnlyMemory<byte> audio)
    {
        if (_sttProvider is null)
        {
            VoiceTranscriptText.Text = "No local STT provider configured.";
            return;
        }

        VoiceRecognitionResult result;
        try
        {
            result = await _sttProvider.TranscribeAsync(audio).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // UI boundary: surface transcription failure honestly.
        catch (Exception)
        {
            VoiceTranscriptText.Text = "Transcription failed.";
            return;
        }
#pragma warning restore CA1031

        if (!result.Success || string.IsNullOrWhiteSpace(result.Transcript))
        {
            VoiceTranscriptText.Text = result.ErrorMessage ?? "No transcript (local STT not configured).";
            VoiceCommandResultText.Text = string.Empty;
            return;
        }

        VoiceTranscriptText.Text = result.Transcript;

        if (!_voiceCommandsEnabled || _voiceCommands is null)
        {
            VoiceCommandResultText.Text = "Transcript ready. Voice commands are off.";
            return;
        }

        // Dispatch the real transcript through the SAME runtime/permission/audit
        // pipeline as a typed command — voice never bypasses it.
        var request = new CommandRequest
        {
            Text = result.Transcript,
            Source = $"voice/{result.ProviderName}",
        };
        var commandResult = await _runtime.DispatchAsync(request).ConfigureAwait(true);
        VoiceCommandResultText.Text = $"Dispatched · {commandResult.Status}";
        await RefreshRecentAsync().ConfigureAwait(true);
    }

    private async Task ResetVoiceIdleAsync()
    {
        _voiceSession = _voiceSession?.WithState(VoiceInteractionState.Idle);
        SetVoiceUi(VoiceInteractionState.Idle, "IDLE");
        await PublishVoiceAsync("Idle.").ConfigureAwait(true);
    }

    private void SetVoiceUi(VoiceInteractionState state, string? badgeOverride = null)
    {
        VoiceStateText.Text = badgeOverride ?? VoiceStateVisualMapper.ChipLabel(state);
        VoiceChipText.Text = $"VOICE · {VoiceStateVisualMapper.ChipLabel(state)}";
        PushToTalkButton.IsEnabled = state != VoiceInteractionState.Listening;
        StopVoiceButton.IsEnabled = state == VoiceInteractionState.Listening;

        // Colour the chip to match the state (M4.6).
        var styleKey = state switch
        {
            VoiceInteractionState.Listening => "VayuChipTeal",
            VoiceInteractionState.Transcribing or VoiceInteractionState.Thinking or VoiceInteractionState.Executing => "VayuChip",
            VoiceInteractionState.Speaking => "VayuChipTeal",
            VoiceInteractionState.Error => "VayuChipRed",
            VoiceInteractionState.Cancelled => "VayuChipAmber",
            _ => "VayuChip",
        };
        if (Application.Current.Resources[styleKey] is Style style)
        {
            VoiceStateBadge.Style = style;
        }

        Sphere.SetVoiceState(state);
    }

    private async Task PublishVoiceAsync(string message)
    {
        if (_voiceSink is null || _voiceSession is null)
        {
            return;
        }
        await _voiceSink.PublishAsync(
            VoiceEvent.FromState(_voiceSession, message, DateTimeOffset.UtcNow)).ConfigureAwait(true);
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        await DispatchAsync().ConfigureAwait(true);
    }

    private async void CommandBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await DispatchAsync().ConfigureAwait(true);
        }
    }

    private async void OnExampleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string text)
        {
            CommandBox.Text = text;
            await DispatchAsync().ConfigureAwait(true);
        }
    }

    private async Task DispatchAsync()
    {
        var text = CommandBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ResultStatus.Text = "LAST RESULT · EMPTY";
            ResultMessage.Text = "Type a command first.";
            return;
        }

        SetRuntime("PROCESSING", VayuAccent.Blue);
        Sphere.SetState(VayuSphereState.Processing);
        ActivityNowText.Text = $"Running: “{text}”";

        var request = new CommandRequest { Text = text, Source = "text" };
        try
        {
            var result = await _runtime.DispatchAsync(request).ConfigureAwait(true);
            RenderResult(result);
            await RefreshRecentAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Sphere.SetState(VayuSphereState.Error);
            SetRuntime("ERROR", VayuAccent.Red);
            ActivityNowText.Text = "Idle — last command errored.";
            ResultStatus.Text = "LAST RESULT · ERROR";
            ResultIndicator.Fill = SolidBrush(VayuAccent.Red);
            ResultAgent.Text = string.Empty;
            ResultRisk.Text = string.Empty;
            ResultMessage.Text = $"{ex.GetType().Name}: {ex.Message}";
            ResultTimestamp.Text = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    private void RenderResult(CommandResult result)
    {
        VayuAccent accent;
        VayuSphereState sphereState;
        string statusLabel;

        switch (result.Status)
        {
            case CommandStatus.Success:
                accent = VayuAccent.Teal;
                sphereState = VayuSphereState.Success;
                statusLabel = "SUCCESS";
                break;
            case CommandStatus.NeedsClarification:
                accent = VayuAccent.Violet;
                sphereState = VayuSphereState.Idle;
                statusLabel = "NEEDS CLARIFICATION";
                break;
            case CommandStatus.PermissionRequired:
                accent = VayuAccent.Amber;
                sphereState = VayuSphereState.Idle;
                statusLabel = "PERMISSION REQUIRED";
                break;
            case CommandStatus.Cancelled:
                accent = VayuAccent.Amber;
                sphereState = VayuSphereState.Error;
                statusLabel = "CANCELLED";
                break;
            case CommandStatus.Failed:
            default:
                accent = VayuAccent.Red;
                sphereState = VayuSphereState.Error;
                statusLabel = "FAILED";
                break;
        }

        Sphere.SetState(sphereState);
        SetRuntime(statusLabel, accent);

        ResultStatus.Text = $"LAST RESULT · {statusLabel}";
        ResultIndicator.Fill = SolidBrush(accent);
        ResultAgent.Text = result.AgentName is null ? string.Empty : $"agent: {result.AgentName}";
        ResultRisk.Text  = result.Plan is null ? string.Empty : $"risk: {result.Plan.Risk}";
        ResultMessage.Text = result.Message ?? result.ClarificationPrompt ?? string.Empty;
        ResultTimestamp.Text = DateTime.Now.ToString("HH:mm:ss");
        ActivityNowText.Text = $"Idle — last result: {statusLabel}.";
    }

    private void SetRuntime(string label, VayuAccent accent)
    {
        RuntimeChipText.Text = $"RUNTIME · {label}";
        RuntimeDot.Fill = SolidBrush(accent);
    }

    private async Task RefreshRecentAsync()
    {
        try
        {
            var rows = await _audit.ListRecentAsync(6).ConfigureAwait(true);
            RecentRows.Clear();
            foreach (var r in rows)
            {
                RecentRows.Add(
                    $"{r.TimestampUtc.UtcDateTime:HH:mm:ss}  {r.RiskLevel,-2}  {r.Status,-19}  {r.AgentName ?? "-",-13}  {r.CommandText ?? string.Empty}");
            }
            RecentCountText.Text = rows.Count == 0 ? string.Empty : $"({rows.Count} most recent)";
            RecentEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            // The audit log is local-only — failures here are non-fatal for the UI.
        }
    }

    private enum VayuAccent { Cyan, Blue, Teal, Violet, Amber, Red }

    private static SolidColorBrush SolidBrush(VayuAccent accent)
    {
        var c = accent switch
        {
            VayuAccent.Cyan   => Windows.UI.Color.FromArgb(0xFF, 0x00, 0xE1, 0xFF),
            VayuAccent.Blue   => Windows.UI.Color.FromArgb(0xFF, 0x4F, 0xA8, 0xFF),
            VayuAccent.Teal   => Windows.UI.Color.FromArgb(0xFF, 0x2E, 0xE6, 0xC9),
            VayuAccent.Violet => Windows.UI.Color.FromArgb(0xFF, 0x9B, 0x7B, 0xFF),
            VayuAccent.Amber  => Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xB8, 0x5C),
            VayuAccent.Red    => Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x5C, 0x7C),
            _ => Colors.White,
        };
        return new SolidColorBrush(c);
    }
}
