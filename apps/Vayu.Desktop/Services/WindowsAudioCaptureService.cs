using System.Runtime.InteropServices;

using Vayu.Voice;

using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

namespace Vayu_Desktop.Services;

/// <summary>
/// Real push-to-talk microphone capture via <see cref="AudioGraph"/>. This is
/// the only place the Windows audio stack is touched; the provider-neutral
/// contract lives in <see cref="IAudioCaptureService"/> so the library stays
/// native-free.
/// </summary>
/// <remarks>
/// Safety properties:
/// <list type="bullet">
/// <item><b>Push-to-talk only.</b> Capture runs only inside an explicit
/// <see cref="StartCaptureAsync"/> call and ends when the caller cancels the
/// token (Stop/Cancel) or the <see cref="LocalSpeechToTextOptions.MaxCaptureSeconds"/>
/// cap elapses.</item>
/// <item><b>In-memory only.</b> Frames are accumulated as 16 kHz mono 16-bit PCM
/// in a <see cref="MemoryStream"/>; nothing is written to disk or logged.</item>
/// <item><b>No cloud.</b> Audio never leaves the device.</item>
/// </list>
/// </remarks>
public sealed class WindowsAudioCaptureService : IAudioCaptureService
{
    private const int TargetSampleRate = 16000; // Whisper expects 16 kHz mono.
    private const int TargetChannels = 1;

    private readonly LocalSpeechToTextOptions _options;

    public WindowsAudioCaptureService(LocalSpeechToTextOptions? options = null)
    {
        _options = options ?? new LocalSpeechToTextOptions();
    }

    /// <inheritdoc />
    public async Task<MicrophoneStatus> GetMicrophoneStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var settings = new AudioGraphSettings(AudioRenderCategory.Speech);
            var result = await AudioGraph.CreateAsync(settings).AsTask(cancellationToken).ConfigureAwait(true);
            if (result.Status != AudioGraphCreationStatus.Success || result.Graph is null)
            {
                return MicrophoneStatus.Unavailable("No usable audio capture device was found.");
            }
            result.Graph.Dispose();
            return MicrophoneStatus.Ready(message: "Microphone is available for push-to-talk.");
        }
#pragma warning disable CA1031 // Status probing must never throw into the caller.
        catch (Exception)
        {
            return MicrophoneStatus.Unavailable("Microphone status could not be determined.");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task<AudioCaptureResult> StartCaptureAsync(CancellationToken cancellationToken = default)
    {
        var maxSeconds = Math.Clamp(_options.MaxCaptureSeconds, 1, 120);

        AudioGraph? graph = null;
        AudioDeviceInputNode? input = null;
        AudioFrameOutputNode? output = null;
        var pcm = new MemoryStream();
        var start = DateTimeOffset.UtcNow;

        try
        {
            var settings = new AudioGraphSettings(AudioRenderCategory.Speech)
            {
                EncodingProperties = AudioEncodingProperties.CreatePcm(
                    (uint)TargetSampleRate, (uint)TargetChannels, 16),
            };

            var created = await AudioGraph.CreateAsync(settings).AsTask(cancellationToken).ConfigureAwait(true);
            if (created.Status != AudioGraphCreationStatus.Success || created.Graph is null)
            {
                return AudioCaptureResult.Failed("Could not create the audio capture graph.");
            }
            graph = created.Graph;

            var inputResult = await graph.CreateDeviceInputNodeAsync(MediaCategory.Speech)
                .AsTask(cancellationToken).ConfigureAwait(true);
            if (inputResult.Status != AudioDeviceNodeCreationStatus.Success || inputResult.DeviceInputNode is null)
            {
                return AudioCaptureResult.Failed("Microphone access was denied or no device is available.");
            }
            input = inputResult.DeviceInputNode;

            output = graph.CreateFrameOutputNode();
            input.AddOutgoingConnection(output);

            graph.QuantumProcessed += (g, _) => DrainFrame(output, pcm);

            graph.Start();
            try
            {
                // Run until Stop/Cancel or the hard duration cap.
                using var capTimer = new CancellationTokenSource(TimeSpan.FromSeconds(maxSeconds));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, capTimer.Token);
                await WaitUntilCancelledAsync(linked.Token).ConfigureAwait(true);
            }
            finally
            {
                graph.Stop();
            }

            var durationMs = (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds;
            if (pcm.Length == 0)
            {
                return AudioCaptureResult.Cancelled(durationMs);
            }

            return AudioCaptureResult.Captured(pcm.ToArray(), TargetSampleRate, TargetChannels, durationMs);
        }
#pragma warning disable CA1031 // Capture must fail safely, never crash the app.
        catch (Exception)
        {
            return AudioCaptureResult.Failed("Microphone capture failed.");
        }
#pragma warning restore CA1031
        finally
        {
            output?.Dispose();
            input?.Dispose();
            graph?.Dispose();
            pcm.Dispose();
        }
    }

    private static void DrainFrame(AudioFrameOutputNode output, MemoryStream pcm)
    {
        using var frame = output.GetFrame();
        using var buffer = frame.LockBuffer(global::Windows.Media.AudioBufferAccessMode.Read);
        using var reference = buffer.CreateReference();

        unsafe
        {
            ((IMemoryBufferByteAccess)reference).GetBuffer(out var dataInBytes, out var capacityInBytes);
            var floatCount = (int)(capacityInBytes / sizeof(float));
            var floats = new Span<float>(dataInBytes, floatCount);
            Span<byte> sample = stackalloc byte[2];
            foreach (var f in floats)
            {
                var clamped = Math.Clamp(f, -1f, 1f);
                var s16 = (short)(clamped * short.MaxValue);
                sample[0] = (byte)(s16 & 0xFF);
                sample[1] = (byte)((s16 >> 8) & 0xFF);
                pcm.Write(sample);
            }
        }
    }

    private static async Task WaitUntilCancelledAsync(CancellationToken token)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (token.Register(static s => ((TaskCompletionSource)s!).TrySetResult(), tcs))
        {
            await tcs.Task.ConfigureAwait(false);
        }
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMemoryBufferByteAccess
    {
        unsafe void GetBuffer(out byte* buffer, out uint capacity);
    }
}
