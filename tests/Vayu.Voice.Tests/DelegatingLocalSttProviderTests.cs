using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class DelegatingLocalSttProviderTests
{
    private static readonly ReadOnlyMemory<byte> SomeAudio = new byte[] { 1, 2, 3, 4 };

    [Fact]
    public async Task GetStatus_NotConfigured_WhenLocalSttOff()
    {
        var provider = new DelegatingLocalSttProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = false },
            transcribe: Never());

        var status = await provider.GetStatusAsync();

        Assert.False(status.IsConfigured);
    }

    [Fact]
    public async Task GetStatus_NotConfigured_WhenModelMissing()
    {
        var provider = new DelegatingLocalSttProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = true, ModelPath = @"C:\nope\model.bin" },
            transcribe: Never(),
            modelExists: _ => false);

        var status = await provider.GetStatusAsync();

        Assert.False(status.IsConfigured);
    }

    [Fact]
    public async Task GetStatus_RuntimeUnavailable_WhenModelPresentButNoDelegate()
    {
        var provider = new DelegatingLocalSttProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = true, ModelPath = @"C:\m\model.bin" },
            transcribe: null,
            modelExists: _ => true);

        var status = await provider.GetStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.False(status.IsAvailable);
        Assert.Contains("runtime", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatus_Ready_WhenEnabledModelAndDelegate()
    {
        var provider = new DelegatingLocalSttProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = true, ModelPath = @"C:\m\model.bin" },
            transcribe: Never(),
            modelExists: _ => true);

        var status = await provider.GetStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.True(status.IsAvailable);
    }

    [Fact]
    public async Task Transcribe_NotConfigured_Fails_WithoutCallingDelegate()
    {
        var called = false;
        var provider = new DelegatingLocalSttProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = false },
            transcribe: (_, _, _) => { called = true; return Task.FromResult(Ok()); });

        var result = await provider.TranscribeAsync(SomeAudio);

        Assert.False(result.Success);
        Assert.False(called); // never ran the engine, never faked a transcript
    }

    [Fact]
    public async Task Transcribe_EmptyAudio_Fails_WithoutCallingDelegate()
    {
        var called = false;
        var provider = new DelegatingLocalSttProvider(
            new LocalSpeechToTextOptions { EnableLocalStt = true, ModelPath = @"C:\m\model.bin" },
            transcribe: (_, _, _) => { called = true; return Task.FromResult(Ok()); },
            modelExists: _ => true);

        var result = await provider.TranscribeAsync(ReadOnlyMemory<byte>.Empty);

        Assert.False(result.Success);
        Assert.False(called);
    }

    [Fact]
    public async Task Transcribe_Configured_RunsRealDelegate_AndReturnsItsResult()
    {
        ReadOnlyMemory<byte> seen = default;
        string? seenModel = null;
        var provider = new DelegatingLocalSttProvider(
            new LocalSpeechToTextOptions
            {
                EnableLocalStt = true,
                ModelPath = @"C:\m\model.bin",
                PreferredProvider = "whisper",
            },
            transcribe: (audio, model, _) =>
            {
                seen = audio;
                seenModel = model;
                return Task.FromResult(
                    VoiceRecognitionResult.Succeeded("open notepad", 0.9, 12, "whisper"));
            },
            modelExists: _ => true);

        var result = await provider.TranscribeAsync(SomeAudio);

        Assert.True(result.Success);
        Assert.Equal("open notepad", result.Transcript);
        Assert.Equal(4, seen.Length);          // the real audio reached the engine
        Assert.Equal(@"C:\m\model.bin", seenModel);
    }

    private static DelegatingLocalSttProvider.TranscribeDelegate Never()
        => (_, _, _) => throw new InvalidOperationException("delegate must not be called");

    private static VoiceRecognitionResult Ok()
        => VoiceRecognitionResult.Succeeded("x", 1.0, 1, "whisper");
}
