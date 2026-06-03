using System.Runtime.InteropServices;
using System.Text.Json;

using Vayu.Voice;
using Vosk;

using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

namespace Vayu_Desktop.Services;

/// <summary>
/// Real local "Hey Vayu" wake word using <b>Vosk</b> (offline, on-device). While
/// armed, it captures 16 kHz mono audio via <see cref="AudioGraph"/>, feeds it to
/// a Vosk recogniser, and routes recognised text through the pure
/// <see cref="WakeWordStateMachine"/>. On a real phrase match it raises
/// <see cref="Triggered"/>. It never logs raw audio, never arms itself, and
/// reports an honest <see cref="WakeWordState.Error"/> when the model is missing.
/// </summary>
public sealed class VoskWakeWordEngine : IWakeWordService, IDisposable
{
    private readonly WakeWordOptions _options;
    private readonly WakeWordStateMachine _machine;
    private readonly object _gate = new();

    private AudioGraph? _graph;
    private AudioDeviceInputNode? _input;
    private AudioFrameOutputNode? _output;
    private Model? _model;
    private VoskRecognizer? _recognizer;
    private bool _listening;

    public VoskWakeWordEngine(WakeWordOptions options)
    {
        _options = options ?? new WakeWordOptions();
        _machine = new WakeWordStateMachine(_options);
    }

    /// <inheritdoc />
    public WakeWordState State => _machine.State;

    /// <inheritdoc />
    public string StatusMessage => _machine.Message;

    /// <inheritdoc />
    public event EventHandler<WakeWordTriggeredEventArgs>? Triggered;

    /// <inheritdoc />
    public event EventHandler<WakeWordState>? StateChanged;

    /// <inheritdoc />
    public async Task<WakeWordState> StartAsync(CancellationToken cancellationToken = default)
    {
        // Arm (pure state machine validates enabled + model present).
        var armed = _machine.Arm();
        RaiseState();
        if (armed != WakeWordState.Armed)
        {
            return armed; // Disabled or Error (honest) — do not open the mic.
        }

        try
        {
            _model = new Model(_options.ModelPath);
            _recognizer = new VoskRecognizer(_model, 16000.0f);
            await StartCaptureAsync(cancellationToken).ConfigureAwait(true);
            _listening = true;
            return WakeWordState.Armed;
        }
#pragma warning disable CA1031 // Wake init must fail safe to a clear Error, never crash.
        catch (Exception)
        {
            await StopAsync().ConfigureAwait(true);
            _machine.Disable();
            // Reflect runtime failure honestly.
            RaiseState();
            return WakeWordState.Error;
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        lock (_gate)
        {
            _listening = false;
            try { _graph?.Stop(); } catch { /* best-effort */ }
            _output?.Dispose();
            _input?.Dispose();
            _graph?.Dispose();
            _recognizer?.Dispose();
            _model?.Dispose();
            _output = null;
            _input = null;
            _graph = null;
            _recognizer = null;
            _model = null;
        }
        _machine.Disable();
        RaiseState();
        return Task.CompletedTask;
    }

    private async Task StartCaptureAsync(CancellationToken cancellationToken)
    {
        var settings = new AudioGraphSettings(AudioRenderCategory.Speech)
        {
            EncodingProperties = AudioEncodingProperties.CreatePcm(16000, 1, 16),
        };
        var created = await AudioGraph.CreateAsync(settings).AsTask(cancellationToken).ConfigureAwait(true);
        if (created.Status != AudioGraphCreationStatus.Success || created.Graph is null)
        {
            throw new InvalidOperationException("audio graph");
        }
        _graph = created.Graph;

        var inputResult = await _graph.CreateDeviceInputNodeAsync(MediaCategory.Speech)
            .AsTask(cancellationToken).ConfigureAwait(true);
        if (inputResult.Status != AudioDeviceNodeCreationStatus.Success || inputResult.DeviceInputNode is null)
        {
            throw new InvalidOperationException("mic");
        }
        _input = inputResult.DeviceInputNode;
        _output = _graph.CreateFrameOutputNode();
        _input.AddOutgoingConnection(_output);
        _graph.QuantumProcessed += (_, _) => ProcessFrame();
        _graph.Start();
    }

    private void ProcessFrame()
    {
        if (!_listening || _recognizer is null || _output is null)
        {
            return;
        }

        byte[]? pcm = null;
        try
        {
            using var frame = _output.GetFrame();
            using var buffer = frame.LockBuffer(global::Windows.Media.AudioBufferAccessMode.Read);
            using var reference = buffer.CreateReference();
            unsafe
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var data, out var capacity);
                var floatCount = (int)(capacity / sizeof(float));
                var floats = new Span<float>(data, floatCount);
                pcm = new byte[floatCount * 2];
                for (var i = 0; i < floatCount; i++)
                {
                    var s16 = (short)(Math.Clamp(floats[i], -1f, 1f) * short.MaxValue);
                    pcm[i * 2] = (byte)(s16 & 0xFF);
                    pcm[(i * 2) + 1] = (byte)((s16 >> 8) & 0xFF);
                }
            }
        }
#pragma warning disable CA1031 // A bad frame must not break the listen loop.
        catch (Exception)
        {
            return;
        }
#pragma warning restore CA1031

        if (pcm is null || pcm.Length == 0)
        {
            return;
        }

        try
        {
            if (_recognizer.AcceptWaveform(pcm, pcm.Length))
            {
                Evaluate(ExtractText(_recognizer.Result()));
            }
            else
            {
                Evaluate(ExtractText(_recognizer.PartialResult(), "partial"));
            }
        }
#pragma warning disable CA1031 // Recogniser failure must not break the loop.
        catch (Exception)
        {
            // Ignore this frame.
        }
#pragma warning restore CA1031
    }

    private void Evaluate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        if (_machine.OnRecognized(text))
        {
            RaiseState();
            Triggered?.Invoke(this, new WakeWordTriggeredEventArgs { RecognizedPhrase = "hey vayu" });
            // Re-arm after the cooldown so repeated triggers don't stack.
            _ = ReArmAfterCooldownAsync();
        }
    }

    private async Task ReArmAfterCooldownAsync()
    {
        await Task.Delay(_options.CooldownMs).ConfigureAwait(true);
        _recognizer?.Reset();
        _machine.ReArm();
        RaiseState();
    }

    private static string? ExtractText(string json, string field = "text")
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(field, out var v) ? v.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void RaiseState() => StateChanged?.Invoke(this, _machine.State);

    public void Dispose() => _ = StopAsync();

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMemoryBufferByteAccess
    {
        unsafe void GetBuffer(out byte* buffer, out uint capacity);
    }
}
