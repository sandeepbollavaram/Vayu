using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class WakeWordStateMachineTests
{
    [Fact]
    public void Arm_WhenDisabled_StaysDisabled()
    {
        var sm = new WakeWordStateMachine(new WakeWordOptions { EnableWakeWord = false });

        Assert.Equal(WakeWordState.Disabled, sm.Arm());
    }

    [Fact]
    public void Arm_EnabledButNoModel_IsError_NotFakeReady()
    {
        var sm = new WakeWordStateMachine(
            new WakeWordOptions { EnableWakeWord = true, ModelPath = @"C:\nope\model" },
            modelExists: _ => false);

        Assert.Equal(WakeWordState.Error, sm.Arm());
        Assert.Contains("not configured", sm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arm_EnabledWithModel_IsArmed()
    {
        var sm = new WakeWordStateMachine(
            new WakeWordOptions { EnableWakeWord = true, ModelPath = @"C:\m\model" },
            modelExists: _ => true);

        Assert.Equal(WakeWordState.Armed, sm.Arm());
    }

    [Fact]
    public void OnRecognized_WhenNotArmed_NeverTriggers()
    {
        var sm = new WakeWordStateMachine(new WakeWordOptions { EnableWakeWord = false });

        Assert.False(sm.OnRecognized("hey vayu"));
        Assert.Equal(WakeWordState.Disabled, sm.State);
    }

    [Fact]
    public void OnRecognized_WakePhrase_WhenArmed_Triggers()
    {
        var sm = Armed();

        Assert.True(sm.OnRecognized("ok hey vayu please"));
        Assert.Equal(WakeWordState.Triggered, sm.State);
    }

    [Theory]
    [InlineData("hello there")]
    [InlineData("vayou")]
    [InlineData("")]
    [InlineData(null)]
    public void OnRecognized_NonWakeText_DoesNotTrigger(string? text)
    {
        var sm = Armed();

        Assert.False(sm.OnRecognized(text));
        Assert.Equal(WakeWordState.Armed, sm.State);
    }

    [Fact]
    public void ReArm_AfterTrigger_ReturnsToArmed()
    {
        var sm = Armed();
        sm.OnRecognized("hey vayu");

        Assert.Equal(WakeWordState.Armed, sm.ReArm());
    }

    [Fact]
    public void Disable_TurnsOff()
    {
        var sm = Armed();

        Assert.Equal(WakeWordState.Disabled, sm.Disable());
    }

    [Theory]
    [InlineData("hey vayu", true)]
    [InlineData("HEY VAYU", true)]
    [InlineData("vayu", true)]
    [InlineData("computer", false)]
    public void ContainsWakePhrase_MatchesConfiguredPhrases(string text, bool expected)
    {
        var options = new WakeWordOptions();
        Assert.Equal(expected, WakeWordStateMachine.ContainsWakePhrase(text, options));
    }

    private static WakeWordStateMachine Armed()
    {
        var sm = new WakeWordStateMachine(
            new WakeWordOptions { EnableWakeWord = true, ModelPath = @"C:\m\model" },
            modelExists: _ => true);
        sm.Arm();
        return sm;
    }
}
