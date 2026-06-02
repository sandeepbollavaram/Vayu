namespace Vayu.Voice;

/// <summary>How a push-to-talk capture ended.</summary>
public enum AudioCaptureStatus
{
    /// <summary>Audio was captured and is ready to transcribe.</summary>
    Captured = 0,

    /// <summary>The user stopped/cancelled before any usable audio was captured.</summary>
    Cancelled = 1,

    /// <summary>Capture failed (no device, permission denied, runtime error).</summary>
    Failed = 2,
}

/// <summary>
/// The outcome of a single push-to-talk microphone capture. The captured audio
/// lives only in <see cref="Pcm16"/> (in memory) and is never written to disk
/// or logged. <see cref="ToString"/> deliberately reveals only metadata, never
/// the bytes, so an accidental log line can't leak audio.
/// </summary>
public sealed class AudioCaptureResult
{
    private readonly ReadOnlyMemory<byte> _pcm16;

    private AudioCaptureResult(
        AudioCaptureStatus status,
        ReadOnlyMemory<byte> pcm16,
        int sampleRate,
        int channels,
        long durationMs,
        string? errorMessage)
    {
        Status = status;
        _pcm16 = pcm16;
        SampleRate = sampleRate;
        Channels = channels;
        DurationMs = durationMs;
        ErrorMessage = errorMessage;
    }

    /// <summary>How the capture ended.</summary>
    public AudioCaptureStatus Status { get; }

    /// <summary>
    /// Raw little-endian 16-bit PCM samples. Empty unless <see cref="Status"/>
    /// is <see cref="AudioCaptureStatus.Captured"/>. In-memory only.
    /// </summary>
    public ReadOnlyMemory<byte> Pcm16 => _pcm16;

    /// <summary>Sample rate of the captured audio (e.g. 16000 Hz for Whisper).</summary>
    public int SampleRate { get; }

    /// <summary>Channel count (1 = mono).</summary>
    public int Channels { get; }

    /// <summary>Captured duration in milliseconds.</summary>
    public long DurationMs { get; }

    /// <summary>Redaction-safe failure reason; null unless <see cref="Status"/> is Failed.</summary>
    public string? ErrorMessage { get; }

    /// <summary>True when there is usable audio to transcribe.</summary>
    public bool HasAudio => Status == AudioCaptureStatus.Captured && !_pcm16.IsEmpty;

    /// <summary>Builds a successful capture result around in-memory PCM samples.</summary>
    public static AudioCaptureResult Captured(ReadOnlyMemory<byte> pcm16, int sampleRate, int channels, long durationMs)
    {
        if (pcm16.IsEmpty)
        {
            throw new ArgumentException("Captured audio must be non-empty.", nameof(pcm16));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        return new AudioCaptureResult(AudioCaptureStatus.Captured, pcm16, sampleRate, channels, durationMs, null);
    }

    /// <summary>A cancelled capture (Stop pressed, or nothing captured). Carries no audio.</summary>
    public static AudioCaptureResult Cancelled(long durationMs = 0)
        => new(AudioCaptureStatus.Cancelled, ReadOnlyMemory<byte>.Empty, 0, 0, durationMs, null);

    /// <summary>A failed capture with a redaction-safe reason. Carries no audio.</summary>
    public static AudioCaptureResult Failed(string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new AudioCaptureResult(AudioCaptureStatus.Failed, ReadOnlyMemory<byte>.Empty, 0, 0, 0, errorMessage);
    }

    /// <summary>Metadata only — never the audio bytes. Safe to log.</summary>
    public override string ToString()
        => $"AudioCaptureResult(Status={Status}, Bytes={_pcm16.Length}, SampleRate={SampleRate}, Channels={Channels}, DurationMs={DurationMs})";
}
