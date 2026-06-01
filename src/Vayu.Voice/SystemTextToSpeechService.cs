using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Vayu.Voice;

/// <summary>
/// System (OS) text-to-speech, structured so the testable parts live here and
/// the WinRT speech engine is injected as a delegate by the desktop app. The
/// library makes <b>no cloud call</b>, saves no audio file, and never speaks a
/// blank, over-long, or secret-looking string.
/// </summary>
/// <remarks>
/// Speaking is opt-in (<see cref="TextToSpeechOptions.EnableTextToSpeech"/>).
/// The <c>speakAsync</c> delegate performs the actual playback; the desktop app
/// supplies a <c>Windows.Media.SpeechSynthesis</c>-backed implementation, while
/// CI/tests use the default no-op so no WinRT dependency is needed.
/// </remarks>
public sealed partial class SystemTextToSpeechService : ITextToSpeechService
{
    /// <summary>Stable provider id used in results/logs.</summary>
    public const string ProviderId = "system";

    private readonly TextToSpeechOptions _options;
    private readonly Func<string, double, double, CancellationToken, Task> _speakAsync;
    private readonly Func<Task> _stopAsync;
    private readonly Func<bool>? _isEnabledOverride;

    /// <summary>Default constructor — speaking is a no-op (safe for CI and headless contexts).</summary>
    public SystemTextToSpeechService(TextToSpeechOptions? options = null)
        : this(options, speakAsync: null, stopAsync: null)
    {
    }

    /// <summary>
    /// Constructs with an injected speech engine. <paramref name="speakAsync"/>
    /// receives (text, rate, volume, ct); <paramref name="stopAsync"/> stops playback.
    /// <paramref name="isEnabledOverride"/>, when supplied, lets a live UI toggle
    /// flip the opt-in at runtime (preferred over <see cref="TextToSpeechOptions.EnableTextToSpeech"/>).
    /// </summary>
    public SystemTextToSpeechService(
        TextToSpeechOptions? options,
        Func<string, double, double, CancellationToken, Task>? speakAsync,
        Func<Task>? stopAsync,
        Func<bool>? isEnabledOverride = null)
    {
        _options = options ?? new TextToSpeechOptions();
        _speakAsync = speakAsync ?? ((_, _, _, _) => Task.CompletedTask);
        _stopAsync = stopAsync ?? (() => Task.CompletedTask);
        _isEnabledOverride = isEnabledOverride;
    }

    private bool Enabled => _isEnabledOverride?.Invoke() ?? _options.EnableTextToSpeech;

    /// <summary>Reports whether TTS is available and enabled. Never speaks.</summary>
    public Task<TextToSpeechProviderStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Enabled
            ? TextToSpeechProviderStatus.Enabled(ProviderId, _options.VoiceName, "Spoken responses are on (System TTS).")
            : TextToSpeechProviderStatus.Disabled(ProviderId, isAvailable: true,
                "Text-to-speech is off by default. Enable it in Settings → Voice."));
    }

    /// <inheritdoc />
    public async Task<SpeechSynthesisResult> SpeakAsync(SpeechSynthesisRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enabled)
        {
            return SpeechSynthesisResult.Failed("Spoken responses are off.", ProviderId, 0);
        }

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return SpeechSynthesisResult.Failed("Nothing to speak.", ProviderId, 0);
        }

        // Never speak secret-looking content (keys, tokens, PEM blocks, bearer headers).
        if (LooksSecret(text))
        {
            return SpeechSynthesisResult.Failed("Refused to speak content that looks like a secret.", ProviderId, 0);
        }

        // Stay terse — cap long text.
        if (_options.MaxCharsPerUtterance > 0 && text.Length > _options.MaxCharsPerUtterance)
        {
            text = text[.._options.MaxCharsPerUtterance];
        }

        var rate = request.Rate ?? _options.Rate;
        var sw = Stopwatch.StartNew();
        try
        {
            await _speakAsync(text, rate, _options.Volume, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return SpeechSynthesisResult.Succeeded(ProviderId, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A speech-engine failure must not crash the caller.
        catch (Exception ex)
        {
            sw.Stop();
            return SpeechSynthesisResult.Failed($"Speech failed: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) => _stopAsync();

    /// <summary>
    /// Heuristic guard so Vayu never reads a secret aloud. Conservative on
    /// purpose — better to stay silent than to speak a key. Public for tests.
    /// </summary>
    public static bool LooksSecret(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var t = text.Trim();
        if (SecretKeywordRegex().IsMatch(t))
        {
            return true;
        }
        if (LongTokenRegex().IsMatch(t))
        {
            return true;
        }
        if (t.Contains("-----BEGIN", StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    // "api key", "apikey", "secret", "token", "password", "bearer ..." cues.
    [GeneratedRegex(@"(\bapi[\s_-]?key\b|\bsecret\b|\bpassword\b|\bbearer\s+\S|\baccess[\s_-]?token\b|\bclient[\s_-]?secret\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretKeywordRegex();

    // A long unbroken high-entropy-ish token (>=24 of letters/digits/-/_/+/.).
    [GeneratedRegex(@"[A-Za-z0-9_\-+./]{24,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongTokenRegex();
}
