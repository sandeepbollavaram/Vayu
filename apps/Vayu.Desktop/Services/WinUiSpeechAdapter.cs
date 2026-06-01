using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Vayu_Desktop.Services;

/// <summary>
/// Desktop adapter that performs the actual speech via Windows System TTS
/// (<see cref="SpeechSynthesizer"/> + <see cref="MediaPlayer"/>). This is the
/// only place the WinRT speech APIs are touched — the testable validation /
/// secret-guard / state lives in <c>Vayu.Voice.SystemTextToSpeechService</c>,
/// which calls into this adapter through a delegate.
/// </summary>
/// <remarks>
/// No cloud call, no audio file: the synthesised stream is played in-process
/// and discarded. <see cref="StopAsync"/> halts playback.
/// </remarks>
public sealed class WinUiSpeechAdapter : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();
    private readonly MediaPlayer _player = new();

    /// <summary>Synthesises and plays <paramref name="text"/>. Rate/volume applied to the player.</summary>
    public async Task SpeakAsync(string text, double rate, double volume, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _synth.Options.SpeakingRate = Clamp(rate, 0.5, 6.0);
        _synth.Options.AudioVolume = Clamp(volume, 0.0, 1.0);

        using var stream = await _synth.SynthesizeTextToStreamAsync(text).AsTask(cancellationToken).ConfigureAwait(true);
        _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
        _player.Play();
    }

    /// <summary>Stops any in-progress speech.</summary>
    public Task StopAsync()
    {
        try
        {
            _player.Pause();
            _player.Source = null;
        }
        catch
        {
            // Best-effort stop.
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _player.Dispose();
        _synth.Dispose();
    }

    private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
}
