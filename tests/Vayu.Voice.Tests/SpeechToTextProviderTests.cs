using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class LocalSpeechToTextOptionsTests
{
    [Fact]
    public void Defaults_AreSafe()
    {
        var o = new LocalSpeechToTextOptions();

        Assert.False(o.EnableLocalStt);
        Assert.Equal("whispercpp", o.PreferredProvider);
        Assert.Null(o.ModelPath);
        Assert.InRange(o.MaxCaptureSeconds, 1, 120);
        Assert.Equal("local-stt", o.ProviderName);
    }
}

public class SpeechToTextProviderStatusTests
{
    [Fact]
    public void NotConfigured_IsSafe()
    {
        var status = SpeechToTextProviderStatus.NotConfigured("whispercpp");

        Assert.False(status.IsAvailable);
        Assert.False(status.IsConfigured);
        Assert.Null(status.ModelPath);
    }

    [Fact]
    public void Ready_RequiresModelPath()
    {
        Assert.ThrowsAny<ArgumentException>(() => SpeechToTextProviderStatus.Ready("whispercpp", "  "));
        var ok = SpeechToTextProviderStatus.Ready("whispercpp", "C:/models/whisper.gguf");
        Assert.True(ok.IsConfigured);
    }
}

public class WhisperCppSpeechToTextProviderTests
{
    private static readonly ReadOnlyMemory<byte> Audio = new byte[] { 1, 2, 3, 4 };

    [Fact]
    public async Task Status_NotConfigured_WhenDisabled()
    {
        var provider = new WhisperCppSpeechToTextProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = false },
            modelExists: _ => true);

        var status = await provider.GetStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.Equal("whispercpp", status.ProviderName);
    }

    [Fact]
    public async Task Status_NotConfigured_WhenModelMissing()
    {
        var provider = new WhisperCppSpeechToTextProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = true, ModelPath = "C:/missing.gguf" },
            modelExists: _ => false);

        var status = await provider.GetStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.Contains("model", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Status_DetectsModel_ButNativeRuntimeNotWired()
    {
        var provider = new WhisperCppSpeechToTextProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = true, ModelPath = "C:/models/whisper.gguf" },
            modelExists: _ => true);

        var status = await provider.GetStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.Equal("C:/models/whisper.gguf", status.ModelPath);
    }

    [Fact]
    public async Task Transcribe_NotConfigured_FailsSafely()
    {
        var provider = new WhisperCppSpeechToTextProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = false },
            modelExists: _ => true);

        var result = await provider.TranscribeAsync(Audio);

        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Transcript);
        Assert.Equal("whispercpp", result.ProviderName);
    }

    [Fact]
    public async Task Transcribe_ModelPresent_ShellNotWired_NeverFakesTranscript()
    {
        var provider = new WhisperCppSpeechToTextProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = true, ModelPath = "C:/models/whisper.gguf" },
            modelExists: _ => true);

        var result = await provider.TranscribeAsync(Audio);

        Assert.False(result.Success);
        Assert.Empty(result.Transcript);
        Assert.Contains("shell is ready", result.ErrorMessage ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transcribe_RespectsCancellation()
    {
        var provider = new WhisperCppSpeechToTextProvider(new LocalSpeechToTextOptions(), modelExists: _ => true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.TranscribeAsync(Audio, cts.Token));
    }
}

/// <summary>
/// A clearly-labelled <b>test-only</b> mock provider. It returns a fixed
/// transcript so the push-to-talk transcription flow can be exercised in tests
/// without a real engine. It is never registered in production.
/// </summary>
public sealed class MockSpeechToTextProvider : ISpeechToTextProvider
{
    private readonly string _transcript;
    public MockSpeechToTextProvider(string transcript = "open notepad") => _transcript = transcript;

    public Task<SpeechToTextProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(SpeechToTextProviderStatus.Ready("mock", "mock://model"));

    public Task<VoiceRecognitionResult> TranscribeAsync(ReadOnlyMemory<byte> audio, CancellationToken cancellationToken = default)
        => Task.FromResult(VoiceRecognitionResult.Succeeded(_transcript, 0.9, 10, "mock"));
}

public class MockSpeechToTextProviderTests
{
    [Fact]
    public async Task Mock_ReturnsConfiguredTranscript()
    {
        var provider = new MockSpeechToTextProvider("show logs");

        var status = await provider.GetStatusAsync();
        var result = await provider.TranscribeAsync(new byte[] { 0 });

        Assert.True(status.IsConfigured);
        Assert.True(result.Success);
        Assert.Equal("show logs", result.Transcript);
        Assert.Equal("mock", result.ProviderName);
    }

    [Fact]
    public void NewProviders_HaveNoAudioOrSecretMember()
    {
        // ISpeechToTextProvider records/statuses must not expose audio/secret fields.
        var statusProps = typeof(SpeechToTextProviderStatus).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Audio", statusProps);
        Assert.DoesNotContain("Secret", statusProps);

        var optProps = typeof(LocalSpeechToTextOptions).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Audio", optProps);
        Assert.DoesNotContain("Key", optProps);
    }
}
