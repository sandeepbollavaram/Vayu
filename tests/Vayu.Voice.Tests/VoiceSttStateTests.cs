using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class VoiceSttStateTests
{
    [Fact]
    public void Default_IsOff_WithNoModelPath()
    {
        var state = new VoiceSttState();

        Assert.False(state.Options.EnableLocalStt);
        Assert.Null(state.Options.ModelPath);
    }

    [Fact]
    public void Configure_EnablesAndSetsModelPath()
    {
        var state = new VoiceSttState();

        state.Configure(@"C:\models\base.bin");

        Assert.True(state.Options.EnableLocalStt);
        Assert.Equal(@"C:\models\base.bin", state.Options.ModelPath);
    }

    [Fact]
    public void Disable_TurnsOff_AndClearsPath()
    {
        var state = new VoiceSttState();
        state.Configure(@"C:\models\base.bin");

        state.Disable();

        Assert.False(state.Options.EnableLocalStt);
        Assert.Null(state.Options.ModelPath);
    }

    [Fact]
    public void Configure_PreservesOtherOptions()
    {
        var state = new VoiceSttState(new LocalSpeechToTextOptions
        {
            PreferredProvider = "whisper",
            MaxCaptureSeconds = 20,
        });

        state.Configure(@"C:\m\model.bin");

        Assert.Equal("whisper", state.Options.PreferredProvider);
        Assert.Equal(20, state.Options.MaxCaptureSeconds);
    }

    [Fact]
    public void Configure_Throws_OnBlankPath()
    {
        var state = new VoiceSttState();

        Assert.ThrowsAny<ArgumentException>(() => state.Configure("  "));
    }
}
