using System.Diagnostics;
using System.Text;

using Vayu.Voice;

using Whisper.net;

namespace Vayu_Desktop.Services;

/// <summary>
/// Real local speech-to-text using <c>whisper.net</c> (CPU runtime). This is the
/// only place the native Whisper runtime is touched; it plugs into the
/// provider-neutral <see cref="DelegatingLocalSttProvider"/> in
/// <c>Vayu.Voice</c> via <see cref="TranscribeAsync"/>, so the library stays
/// native-free.
/// </summary>
/// <remarks>
/// Safety / honesty properties:
/// <list type="bullet">
/// <item>Fully local — whisper.net runs on-device; no audio leaves the machine.</item>
/// <item>Audio is processed as in-memory float samples; nothing is written to
/// disk and no audio is logged.</item>
/// <item>Every failure (missing model, runtime load error, empty audio, empty
/// text, cancellation, native error) maps to a safe
/// <see cref="VoiceRecognitionResult"/> message — no raw exception reaches the
/// UI and no transcript is ever fabricated.</item>
/// </list>
/// </remarks>
public sealed class WhisperNetSpeechToTextEngine
{
    /// <summary>Provider id surfaced in results and the audit source.</summary>
    public const string ProviderId = "whisper.net";

    private const int ExpectedSampleRate = 16000;

    /// <summary>
    /// Transcribes 16 kHz mono 16-bit PCM with the whisper.net model at
    /// <paramref name="modelPath"/>. Matches
    /// <see cref="DelegatingLocalSttProvider.TranscribeDelegate"/>.
    /// </summary>
    public async Task<VoiceRecognitionResult> TranscribeAsync(
        ReadOnlyMemory<byte> audio,
        string modelPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return VoiceRecognitionResult.Failed(
                "No local STT model is configured.", durationMs: 0, providerName: ProviderId);
        }
        if (!File.Exists(modelPath))
        {
            return VoiceRecognitionResult.Failed(
                "The configured local STT model file was not found.", durationMs: 0, providerName: ProviderId);
        }
        if (audio.IsEmpty)
        {
            return VoiceRecognitionResult.Failed(
                "No audio was captured to transcribe.", durationMs: 0, providerName: ProviderId);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return VoiceRecognitionResult.Failed(
                "Transcription was cancelled.", durationMs: 0, providerName: ProviderId);
        }

        float[] samples;
        try
        {
            samples = PcmAudioConverter.Pcm16ToFloat(audio);
        }
        catch (ArgumentException)
        {
            return VoiceRecognitionResult.Failed(
                "Captured audio was not valid 16-bit PCM.", durationMs: 0, providerName: ProviderId);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            // FromPath loads the native model; this throws if the runtime/model
            // can't be loaded — caught below and reported as runtime-unavailable.
            using var factory = WhisperFactory.FromPath(modelPath);
            using var processor = factory.CreateBuilder()
                .WithLanguage("en")
                .Build();

            var builder = new StringBuilder();
            await foreach (var segment in processor
                .ProcessAsync(samples, cancellationToken)
                .ConfigureAwait(false))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    builder.Append(segment.Text);
                }
            }

            stopwatch.Stop();
            var transcript = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return VoiceRecognitionResult.Failed(
                    "No speech was recognised.", stopwatch.ElapsedMilliseconds, ProviderId);
            }

            return VoiceRecognitionResult.Succeeded(
                transcript, confidence: null, stopwatch.ElapsedMilliseconds, ProviderId);
        }
        catch (OperationCanceledException)
        {
            return VoiceRecognitionResult.Failed(
                "Transcription was cancelled.", stopwatch.ElapsedMilliseconds, ProviderId);
        }
#pragma warning disable CA1031 // Native STT boundary: map any engine/runtime failure to a safe message.
        catch (Exception)
        {
            return VoiceRecognitionResult.Failed(
                "The local STT runtime could not transcribe (model or runtime unavailable).",
                stopwatch.ElapsedMilliseconds, ProviderId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Lightweight readiness probe used by Settings: confirms a model file is
    /// present and loadable without running a transcription.
    /// </summary>
    public bool TryProbeRuntime(string? modelPath, out string message)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            message = "No local STT model is configured.";
            return false;
        }
        if (!File.Exists(modelPath))
        {
            message = "The configured local STT model file was not found.";
            return false;
        }
        try
        {
            using var factory = WhisperFactory.FromPath(modelPath);
            message = "Local STT runtime is ready (whisper.net).";
            return true;
        }
#pragma warning disable CA1031 // Probe must never throw; report runtime-unavailable.
        catch (Exception)
        {
            message = "The local STT runtime could not load this model.";
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>Sample rate the capture pipeline must produce (16 kHz).</summary>
    public static int RequiredSampleRate => ExpectedSampleRate;
}
