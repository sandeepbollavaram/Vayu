using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class TextToSpeechOptionsTests
{
    [Fact]
    public void Defaults_AreOffAndSafe()
    {
        var o = new TextToSpeechOptions();

        Assert.False(o.EnableTextToSpeech);
        Assert.Equal("system", o.ProviderName);
        Assert.Equal(1.0, o.Rate);
        Assert.Equal(1.0, o.Volume);
        Assert.InRange(o.MaxCharsPerUtterance, 50, 1000);
    }
}

public class VoiceAssistantPhrasesTests
{
    [Fact]
    public void Phrases_AreShortAndNeutral()
    {
        Assert.All(VoiceAssistantPhrases.All, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p));
            Assert.True(p.Length <= 60);
        });
        Assert.Contains("Done.", VoiceAssistantPhrases.All);
        Assert.Contains("Cancelled.", VoiceAssistantPhrases.All);
    }
}

public class SystemTextToSpeechServiceTests
{
    [Fact]
    public async Task Disabled_ByDefault_DoesNotSpeak()
    {
        var spoke = false;
        var svc = NewService(new TextToSpeechOptions { EnableTextToSpeech = false }, () => spoke = true);

        var result = await svc.SpeakAsync(new SpeechSynthesisRequest("Done."));

        Assert.False(result.Success);
        Assert.False(spoke);
        Assert.Contains("off", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enabled_SpeaksShortPhrase()
    {
        string? spoken = null;
        var svc = NewService(new TextToSpeechOptions { EnableTextToSpeech = true }, captureText: t => spoken = t);

        var result = await svc.SpeakAsync(new SpeechSynthesisRequest(VoiceAssistantPhrases.Done));

        Assert.True(result.Success);
        Assert.Equal("Done.", spoken);
    }

    [Fact]
    public async Task BlankText_Rejected()
    {
        var svc = NewService(new TextToSpeechOptions { EnableTextToSpeech = true });

        var result = await svc.SpeakAsync(new SpeechSynthesisRequest("   "));

        Assert.False(result.Success);
        Assert.Contains("Nothing", result.ErrorMessage);
    }

    [Fact]
    public async Task LongText_IsCapped()
    {
        string? spoken = null;
        var svc = NewService(
            new TextToSpeechOptions { EnableTextToSpeech = true, MaxCharsPerUtterance = 10 },
            captureText: t => spoken = t);

        // A long but ordinary sentence (spaces break it up so it is not a token run).
        var longText = string.Join(" ", Enumerable.Repeat("word", 50));
        var result = await svc.SpeakAsync(new SpeechSynthesisRequest(longText));

        Assert.True(result.Success);
        Assert.Equal(10, spoken!.Length);
    }

    [Theory]
    [InlineData("Your api key is configured")]
    [InlineData("password is hunter2")]
    [InlineData("Bearer abcdEFGH1234")]
    public async Task SecretLooking_Text_IsRefused(string text)
    {
        var svc = NewService(new TextToSpeechOptions { EnableTextToSpeech = true });

        var result = await svc.SpeakAsync(new SpeechSynthesisRequest(text));

        Assert.False(result.Success);
        Assert.Contains("secret", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LongToken_And_PemHeader_AreFlagged()
    {
        // Build the test strings at runtime so no key-shaped literal is committed.
        var longToken = new string('Z', 40);                       // 40-char unbroken token
        var pem = "-----BEGIN " + "PRIVATE KEY" + "-----";          // split so the scanner literal isn't committed

        Assert.True(SystemTextToSpeechService.LooksSecret(longToken));
        Assert.True(SystemTextToSpeechService.LooksSecret(pem));
    }

    [Theory]
    [InlineData("Done.")]
    [InlineData("Cancelled.")]
    [InlineData("I need confirmation to do that.")]
    public void NeutralPhrases_AreNotFlaggedSecret(string phrase)
    {
        Assert.False(SystemTextToSpeechService.LooksSecret(phrase));
    }

    [Fact]
    public async Task Stop_IsSafe_WhenNothingPlaying()
    {
        var svc = NewService(new TextToSpeechOptions { EnableTextToSpeech = true });

        await svc.StopAsync(); // must not throw
    }

    [Fact]
    public async Task Status_Reflects_EnabledFlag()
    {
        var off = await NewService(new TextToSpeechOptions { EnableTextToSpeech = false }).GetStatusAsync();
        var on = await NewService(new TextToSpeechOptions { EnableTextToSpeech = true }).GetStatusAsync();

        Assert.False(off.IsEnabled);
        Assert.True(on.IsEnabled);
        Assert.Equal("system", on.ProviderName);
    }

    [Fact]
    public async Task Speak_RespectsCancellation()
    {
        var svc = new SystemTextToSpeechService(
            new TextToSpeechOptions { EnableTextToSpeech = true },
            speakAsync: (_, _, _, ct) => { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; },
            stopAsync: null);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.SpeakAsync(new SpeechSynthesisRequest("Done."), cts.Token));
    }

    [Fact]
    public void Records_HaveNoSecretOrAudioField()
    {
        var statusProps = typeof(TextToSpeechProviderStatus).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("Audio", statusProps);
        Assert.DoesNotContain("Secret", statusProps);
        Assert.DoesNotContain("Key", statusProps);
    }

    // ---- helpers ----

    private static SystemTextToSpeechService NewService(
        TextToSpeechOptions options,
        Action? onSpeak = null,
        Action<string>? captureText = null)
        => new(
            options,
            speakAsync: (text, _, _, _) =>
            {
                onSpeak?.Invoke();
                captureText?.Invoke(text);
                return Task.CompletedTask;
            },
            stopAsync: null);
}
