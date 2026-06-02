namespace Vayu.Voice;

/// <summary>
/// Converts the in-memory 16-bit mono PCM that <see cref="IAudioCaptureService"/>
/// produces into the normalised <see cref="float"/> samples a local STT engine
/// (e.g. whisper.net) expects. Pure and allocation-bounded — no I/O, no audio
/// written to disk, no audio in any string surface.
/// </summary>
public static class PcmAudioConverter
{
    private const int BytesPerSample = 2; // 16-bit

    /// <summary>
    /// Converts little-endian 16-bit PCM bytes to <c>float</c> samples in the
    /// range [-1, 1]. Throws <see cref="ArgumentException"/> on empty or
    /// odd-length input (a truncated final sample is never silently dropped).
    /// </summary>
    public static float[] Pcm16ToFloat(ReadOnlySpan<byte> pcm16)
    {
        if (pcm16.IsEmpty)
        {
            throw new ArgumentException("PCM audio is empty.", nameof(pcm16));
        }
        if (pcm16.Length % BytesPerSample != 0)
        {
            throw new ArgumentException(
                "PCM audio length must be a multiple of 2 (16-bit samples).", nameof(pcm16));
        }

        var sampleCount = pcm16.Length / BytesPerSample;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var lo = pcm16[i * BytesPerSample];
            var hi = pcm16[(i * BytesPerSample) + 1];
            var s16 = (short)(lo | (hi << 8));
            // short.MinValue (-32768) maps slightly past -1.0; clamp keeps it in range.
            samples[i] = Math.Clamp(s16 / 32768f, -1f, 1f);
        }
        return samples;
    }

    /// <summary>
    /// Convenience overload for <see cref="ReadOnlyMemory{Byte}"/> captured audio.
    /// </summary>
    public static float[] Pcm16ToFloat(ReadOnlyMemory<byte> pcm16) => Pcm16ToFloat(pcm16.Span);
}
