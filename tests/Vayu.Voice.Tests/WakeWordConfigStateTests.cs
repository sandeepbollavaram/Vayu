using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class WakeWordConfigStateTests
{
    [Fact]
    public void Default_IsOff()
    {
        var state = new WakeWordConfigState();

        Assert.False(state.Options.EnableWakeWord);
    }

    [Fact]
    public void Enable_TurnsOn_AndKeepsModelPath()
    {
        var state = new WakeWordConfigState(new WakeWordOptions { ModelPath = @"C:\models\vosk-wake" });

        state.Enable();

        Assert.True(state.Options.EnableWakeWord);
        Assert.Equal(@"C:\models\vosk-wake", state.Options.ModelPath);
    }

    [Fact]
    public void Disable_TurnsOff()
    {
        var state = new WakeWordConfigState(new WakeWordOptions { EnableWakeWord = true });

        state.Disable();

        Assert.False(state.Options.EnableWakeWord);
    }

    [Fact]
    public void SetModelPath_UpdatesPath_WithoutEnabling()
    {
        var state = new WakeWordConfigState();

        state.SetModelPath(@"C:\models\vosk-wake");

        Assert.Equal(@"C:\models\vosk-wake", state.Options.ModelPath);
        Assert.False(state.Options.EnableWakeWord); // setting a path is not consent to listen
    }

    [Fact]
    public void Enable_PreservesPhrasesAndCooldown()
    {
        var state = new WakeWordConfigState(new WakeWordOptions { CooldownMs = 2000 });

        state.Enable();

        Assert.Equal(2000, state.Options.CooldownMs);
        Assert.Contains("hey vayu", state.Options.Phrases);
    }

    [Fact]
    public void SetModelPath_Throws_OnBlankPath()
    {
        var state = new WakeWordConfigState();

        Assert.ThrowsAny<ArgumentException>(() => state.SetModelPath("  "));
    }
}
