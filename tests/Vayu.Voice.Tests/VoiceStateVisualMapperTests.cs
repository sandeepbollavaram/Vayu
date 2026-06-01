using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class VoiceStateVisualMapperTests
{
    [Theory]
    [InlineData(VoiceInteractionState.Idle, VoiceVisualToken.IdlePulse)]
    [InlineData(VoiceInteractionState.Listening, VoiceVisualToken.ListeningPulse)]
    [InlineData(VoiceInteractionState.Transcribing, VoiceVisualToken.TranscribingScan)]
    [InlineData(VoiceInteractionState.Thinking, VoiceVisualToken.ThinkingAccent)]
    [InlineData(VoiceInteractionState.Executing, VoiceVisualToken.ExecutingRing)]
    [InlineData(VoiceInteractionState.Speaking, VoiceVisualToken.SpeakingGlow)]
    [InlineData(VoiceInteractionState.Error, VoiceVisualToken.ErrorFlash)]
    [InlineData(VoiceInteractionState.Cancelled, VoiceVisualToken.CancelledFade)]
    public void Map_ReturnsExpectedToken(VoiceInteractionState state, VoiceVisualToken expected)
    {
        Assert.Equal(expected, VoiceStateVisualMapper.Map(state));
    }

    [Fact]
    public void Map_UnknownState_FallsBackToIdle()
    {
        Assert.Equal(VoiceVisualToken.IdlePulse, VoiceStateVisualMapper.Map((VoiceInteractionState)999));
    }

    [Theory]
    [InlineData(VoiceInteractionState.Idle, "IDLE")]
    [InlineData(VoiceInteractionState.Listening, "LISTENING")]
    [InlineData(VoiceInteractionState.Transcribing, "TRANSCRIBING")]
    [InlineData(VoiceInteractionState.Speaking, "SPEAKING")]
    [InlineData(VoiceInteractionState.Cancelled, "CANCELLED")]
    [InlineData(VoiceInteractionState.Error, "ERROR")]
    public void ChipLabel_ReturnsExpected(VoiceInteractionState state, string expected)
    {
        Assert.Equal(expected, VoiceStateVisualMapper.ChipLabel(state));
    }
}
