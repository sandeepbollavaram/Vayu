using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class AudioCaptureResultTests
{
    [Fact]
    public void Captured_ExposesAudio_AndMetadata()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };

        var result = AudioCaptureResult.Captured(bytes, sampleRate: 16000, channels: 1, durationMs: 250);

        Assert.Equal(AudioCaptureStatus.Captured, result.Status);
        Assert.True(result.HasAudio);
        Assert.Equal(4, result.Pcm16.Length);
        Assert.Equal(16000, result.SampleRate);
        Assert.Equal(1, result.Channels);
        Assert.Equal(250, result.DurationMs);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Cancelled_HasNoAudio()
    {
        var result = AudioCaptureResult.Cancelled(durationMs: 120);

        Assert.Equal(AudioCaptureStatus.Cancelled, result.Status);
        Assert.False(result.HasAudio);
        Assert.True(result.Pcm16.IsEmpty);
        Assert.Equal(120, result.DurationMs);
    }

    [Fact]
    public void Failed_HasNoAudio_AndReason()
    {
        var result = AudioCaptureResult.Failed("device error");

        Assert.Equal(AudioCaptureStatus.Failed, result.Status);
        Assert.False(result.HasAudio);
        Assert.True(result.Pcm16.IsEmpty);
        Assert.Equal("device error", result.ErrorMessage);
    }

    [Fact]
    public void Captured_RejectsEmptyAudio()
    {
        Assert.Throws<ArgumentException>(() =>
            AudioCaptureResult.Captured(ReadOnlyMemory<byte>.Empty, 16000, 1, 0));
    }

    [Fact]
    public void ToString_NeverRevealsAudioBytes()
    {
        // A distinctive byte pattern that must NOT appear in any log line.
        var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        var text = AudioCaptureResult.Captured(bytes, 16000, 1, 100).ToString();

        // Metadata only — the byte count is fine, the bytes themselves are not.
        Assert.Contains("Bytes=4", text);
        Assert.Contains("Status=Captured", text);
        Assert.DoesNotContain("DEAD", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEEF", text, StringComparison.OrdinalIgnoreCase);
    }
}
