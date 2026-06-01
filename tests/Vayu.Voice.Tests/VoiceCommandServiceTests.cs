using Vayu.Core;
using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class VoiceCommandServiceTests
{
    [Fact]
    public async Task FailedTranscript_DoesNotDispatch()
    {
        var runtime = new FakeRuntime();
        var svc = NewService(VoiceRecognitionResult.Failed("no speech", 0, "stub"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true });

        var result = await svc.StartPushToTalkCommandDetailedAsync();

        Assert.False(result.WasDispatched);
        Assert.Null(runtime.LastRequest);
    }

    [Fact]
    public async Task EmptyTranscript_DoesNotDispatch()
    {
        var runtime = new FakeRuntime();
        // Success but blank transcript.
        var svc = NewService(new VoiceRecognitionResult(true, "   ", 0.9, null, 10, "stub"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true });

        var result = await svc.StartPushToTalkCommandDetailedAsync();

        Assert.False(result.WasDispatched);
        Assert.Null(runtime.LastRequest);
    }

    [Fact]
    public async Task LowConfidence_DoesNotDispatch()
    {
        var runtime = new FakeRuntime();
        var svc = NewService(VoiceRecognitionResult.Succeeded("open notepad", 0.20, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true, MinimumConfidence = 0.70 });

        var result = await svc.StartPushToTalkCommandDetailedAsync();

        Assert.False(result.WasDispatched);
        Assert.Contains("confidence", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(runtime.LastRequest);
    }

    [Fact]
    public async Task Disabled_DoesNotDispatch_EvenWithGoodTranscript()
    {
        var runtime = new FakeRuntime();
        var svc = NewService(VoiceRecognitionResult.Succeeded("open notepad", 0.95, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = false });

        var result = await svc.StartPushToTalkCommandDetailedAsync();

        Assert.False(result.WasDispatched);
        Assert.Null(runtime.LastRequest);
    }

    [Fact]
    public async Task GoodTranscript_Dispatches_WithVoiceSource()
    {
        var runtime = new FakeRuntime(CommandResult.Success("opened"));
        var svc = NewService(VoiceRecognitionResult.Succeeded("open notepad", 0.95, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true });

        var result = await svc.StartPushToTalkCommandDetailedAsync();

        Assert.True(result.WasDispatched);
        Assert.NotNull(runtime.LastRequest);
        Assert.Equal("open notepad", runtime.LastRequest!.Text);
        Assert.Equal("voice/whisper", runtime.LastRequest.Source);
        Assert.Equal(CommandStatus.Success, result.CommandResult!.Status);
    }

    [Fact]
    public async Task Dispatch_PreservesCorrelationId_AndMetadata()
    {
        var runtime = new FakeRuntime(CommandResult.Success());
        var svc = NewService(VoiceRecognitionResult.Succeeded("show logs", 0.9, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true });

        var result = await svc.StartPushToTalkCommandDetailedAsync();

        Assert.Equal(result.CorrelationId, runtime.LastRequest!.CorrelationId);
        Assert.Equal("whisper", runtime.LastRequest.Metadata["voice.provider"]);
        Assert.True(runtime.LastRequest.Metadata.ContainsKey("voice.confidence"));
    }

    [Fact]
    public async Task LongTranscript_IsCapped()
    {
        var runtime = new FakeRuntime(CommandResult.Success());
        var longText = new string('a', 1000);
        var svc = NewService(VoiceRecognitionResult.Succeeded(longText, 0.9, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true, MaxTranscriptChars = 50 });

        await svc.StartPushToTalkCommandDetailedAsync();

        Assert.Equal(50, runtime.LastRequest!.Text.Length);
    }

    [Fact]
    public async Task Tts_SpeaksShortPhrase_WhenEnabled()
    {
        var runtime = new FakeRuntime(CommandResult.Success());
        var tts = new FakeTts();
        var state = new VoiceTtsState(initiallyEnabled: true);
        var svc = NewService(VoiceRecognitionResult.Succeeded("open notepad", 0.95, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true }, tts, state);

        await svc.StartPushToTalkCommandDetailedAsync();

        Assert.Equal(VoiceAssistantPhrases.Done, tts.LastSpoken);
        // Never the transcript.
        Assert.NotEqual("open notepad", tts.LastSpoken);
    }

    [Fact]
    public async Task Tts_NotCalled_WhenStateDisabled()
    {
        var runtime = new FakeRuntime(CommandResult.Success());
        var tts = new FakeTts();
        var state = new VoiceTtsState(initiallyEnabled: false);
        var svc = NewService(VoiceRecognitionResult.Succeeded("open notepad", 0.95, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true }, tts, state);

        await svc.StartPushToTalkCommandDetailedAsync();

        Assert.Null(tts.LastSpoken);
    }

    [Fact]
    public async Task NeedsConfirmation_Speaks_ConfirmationPhrase()
    {
        var plan = new IntentPlan { Intent = "app.open", Risk = RiskLevel.L3 };
        var runtime = new FakeRuntime(CommandResult.PermissionRequired(plan));
        var tts = new FakeTts();
        var svc = NewService(VoiceRecognitionResult.Succeeded("type hello", 0.95, 10, "whisper"), runtime,
            new VoiceCommandOptions { EnableVoiceCommands = true }, tts, new VoiceTtsState(true));

        await svc.StartPushToTalkCommandDetailedAsync();

        Assert.Equal(VoiceAssistantPhrases.NeedsConfirmation, tts.LastSpoken);
    }

    [Fact]
    public async Task InterfaceMethod_ReturnsCommandResult()
    {
        var runtime = new FakeRuntime(CommandResult.Success());
        IVoiceCommandService svc = NewService(VoiceRecognitionResult.Succeeded("open notepad", 0.95, 10, "whisper"),
            runtime, new VoiceCommandOptions { EnableVoiceCommands = true });

        var result = await svc.StartPushToTalkCommandAsync();

        Assert.NotNull(result);
        Assert.Equal(CommandStatus.Success, result!.Status);
    }

    [Fact]
    public void Constructor_Throws_OnNullDeps()
    {
        var input = new FakeInput(VoiceRecognitionResult.Failed("x", 0, "stub"));
        var runtime = new FakeRuntime();
        Assert.Throws<ArgumentNullException>(() => new VoiceCommandService(null!, runtime, new FixedClock()));
        Assert.Throws<ArgumentNullException>(() => new VoiceCommandService(input, null!, new FixedClock()));
        Assert.Throws<ArgumentNullException>(() => new VoiceCommandService(input, runtime, null!));
    }

    // ---- helpers ----

    private static VoiceCommandService NewService(
        VoiceRecognitionResult recognition,
        FakeRuntime runtime,
        VoiceCommandOptions options,
        ITextToSpeechService? tts = null,
        VoiceTtsState? state = null)
        => new(new FakeInput(recognition), runtime, new FixedClock(), options, sink: null, tts: tts, ttsState: state);

    private sealed class FakeInput : IVoiceInputService
    {
        private readonly VoiceRecognitionResult _result;
        public FakeInput(VoiceRecognitionResult result) => _result = result;

        public Task<MicrophoneStatus> GetMicrophoneStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(MicrophoneStatus.Ready());

        public Task<VoiceRecognitionResult> StartPushToTalkAsync(VoiceSession session, CancellationToken cancellationToken = default)
            => Task.FromResult(_result);

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRuntime : IAgentRuntime
    {
        private readonly CommandResult _result;
        public FakeRuntime(CommandResult? result = null) => _result = result ?? CommandResult.Success();
        public CommandRequest? LastRequest { get; private set; }

        public Task<CommandResult> DispatchAsync(CommandRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeTts : ITextToSpeechService
    {
        public string? LastSpoken { get; private set; }

        public Task<SpeechSynthesisResult> SpeakAsync(SpeechSynthesisRequest request, CancellationToken cancellationToken = default)
        {
            LastSpoken = request.Text;
            return Task.FromResult(SpeechSynthesisResult.Succeeded("fake", 0));
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    }
}
