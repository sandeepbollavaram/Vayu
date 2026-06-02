using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class PcmAudioConverterTests
{
    [Fact]
    public void Silence_ConvertsToZeros()
    {
        // 4 samples of 16-bit silence (all zero bytes).
        var pcm = new byte[8];

        var floats = PcmAudioConverter.Pcm16ToFloat(pcm);

        Assert.Equal(4, floats.Length);
        Assert.All(floats, f => Assert.Equal(0f, f));
    }

    [Fact]
    public void FullScale_ConvertsWithinRange()
    {
        // max positive (0x7FFF) and max negative (0x8000), little-endian.
        var pcm = new byte[] { 0xFF, 0x7F, 0x00, 0x80 };

        var floats = PcmAudioConverter.Pcm16ToFloat(pcm);

        Assert.Equal(2, floats.Length);
        Assert.InRange(floats[0], 0.9f, 1.0f);    // ~ +32767/32768
        Assert.InRange(floats[1], -1.0f, -0.99f); // -32768/32768 clamped to -1
        Assert.All(floats, f => Assert.InRange(f, -1f, 1f));
    }

    [Fact]
    public void KnownSample_DecodesLittleEndian()
    {
        // 0x0100 little-endian = 256.
        var pcm = new byte[] { 0x00, 0x01 };

        var floats = PcmAudioConverter.Pcm16ToFloat(pcm);

        Assert.Single(floats);
        Assert.Equal(256f / 32768f, floats[0], precision: 5);
    }

    [Fact]
    public void Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => PcmAudioConverter.Pcm16ToFloat(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void OddLength_Throws()
    {
        // 3 bytes is not a whole number of 16-bit samples.
        Assert.Throws<ArgumentException>(() => PcmAudioConverter.Pcm16ToFloat(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void MemoryOverload_MatchesSpanOverload()
    {
        var pcm = new byte[] { 0x10, 0x20, 0x30, 0x40 };

        var fromMemory = PcmAudioConverter.Pcm16ToFloat(new ReadOnlyMemory<byte>(pcm));
        var fromSpan = PcmAudioConverter.Pcm16ToFloat(pcm.AsSpan());

        Assert.Equal(fromSpan, fromMemory);
    }
}
