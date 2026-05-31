using System.Reflection;

using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class VoiceContractsTests
{
    [Theory]
    [InlineData(VoiceInteractionState.Idle)]
    [InlineData(VoiceInteractionState.Listening)]
    [InlineData(VoiceInteractionState.Transcribing)]
    [InlineData(VoiceInteractionState.Thinking)]
    [InlineData(VoiceInteractionState.Executing)]
    [InlineData(VoiceInteractionState.Speaking)]
    [InlineData(VoiceInteractionState.Error)]
    [InlineData(VoiceInteractionState.Cancelled)]
    public void VoiceInteractionState_HasExpectedValues(VoiceInteractionState state)
    {
        Assert.True(Enum.IsDefined(state));
    }

    [Fact]
    public void VoiceInputMode_OnlyPushToTalk_IsActiveInM41()
    {
        Assert.True(VoiceInputModes.IsActive(VoiceInputMode.PushToTalk));
        Assert.False(VoiceInputModes.IsActive(VoiceInputMode.WakeWordPlanned));
        Assert.False(VoiceInputModes.IsActive(VoiceInputMode.ClapTriggerPlanned));
    }

    [Fact]
    public void VoiceSession_StartPushToTalk_IsListening()
    {
        var now = DateTimeOffset.UtcNow;
        var session = VoiceSession.StartPushToTalk(now);

        Assert.Equal(VoiceInteractionState.Listening, session.State);
        Assert.Equal(VoiceInputMode.PushToTalk, session.InputMode);
        Assert.NotEqual(Guid.Empty, session.CorrelationId);
        Assert.Null(session.EndedAtUtc);
    }

    [Fact]
    public void VoiceSession_Cancelled_And_Errored_AreSafe()
    {
        var now = DateTimeOffset.UtcNow;
        var session = VoiceSession.StartPushToTalk(now);

        var cancelled = session.Cancelled(now);
        Assert.Equal(VoiceInteractionState.Cancelled, cancelled.State);
        Assert.NotNull(cancelled.EndedAtUtc);

        var errored = session.Errored("mic unavailable", now);
        Assert.Equal(VoiceInteractionState.Error, errored.State);
        Assert.Equal("mic unavailable", errored.ErrorMessage);
    }

    [Fact]
    public void VoiceRecognitionResult_Success_RequiresTranscript()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => VoiceRecognitionResult.Succeeded("  ", 0.9, 100, "test"));

        var ok = VoiceRecognitionResult.Succeeded("open notepad", 0.9, 120, "test");
        Assert.True(ok.Success);
        Assert.Equal("open notepad", ok.Transcript);
    }

    [Fact]
    public void VoiceRecognitionResult_LowConfidence_IsRepresentable()
    {
        var low = VoiceRecognitionResult.Succeeded("maybe this", 0.12, 80, "test");

        Assert.True(low.Success);
        Assert.True(low.Confidence < 0.5);
    }

    [Fact]
    public void VoiceRecognitionResult_Failed_HasEmptyTranscript()
    {
        var failed = VoiceRecognitionResult.Failed("no speech detected", 50, "test");

        Assert.False(failed.Success);
        Assert.Equal(string.Empty, failed.Transcript);
        Assert.NotNull(failed.ErrorMessage);
    }

    [Fact]
    public void MicrophoneStatus_Unavailable_CannotCapture()
    {
        var status = MicrophoneStatus.Unavailable();

        Assert.False(status.IsAvailable);
        Assert.False(status.PermissionGranted);
        Assert.False(status.CanCapture);
    }

    [Fact]
    public void MicrophoneStatus_Ready_CanCapture()
    {
        var status = MicrophoneStatus.Ready("Default Microphone");

        Assert.True(status.CanCapture);
        Assert.Equal("Default Microphone", status.DeviceName);
    }

    [Fact]
    public void SpeechSynthesisResult_Factories_Work()
    {
        Assert.True(SpeechSynthesisResult.Succeeded("system-tts", 200).Success);
        Assert.False(SpeechSynthesisResult.Failed("voice not found", "system-tts", 10).Success);
    }

    [Fact]
    public void VoiceEvent_FromState_FlagsError()
    {
        var session = VoiceSession.StartPushToTalk(DateTimeOffset.UtcNow).Errored("boom", DateTimeOffset.UtcNow);

        var ev = VoiceEvent.FromState(session, "capture failed", DateTimeOffset.UtcNow);

        Assert.True(ev.IsError);
        Assert.Equal(session.CorrelationId, ev.CorrelationId);
    }

    [Fact]
    public void PublicVoiceRecords_HaveNoRawAudioOrSecretFields()
    {
        var types = new[]
        {
            typeof(VoiceSession),
            typeof(VoiceRecognitionResult),
            typeof(SpeechSynthesisRequest),
            typeof(SpeechSynthesisResult),
            typeof(MicrophoneStatus),
            typeof(VoiceEvent),
        };

        foreach (var type in types)
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            Assert.DoesNotContain("Audio", props);
            Assert.DoesNotContain("AudioBytes", props);
            Assert.DoesNotContain("Pcm", props);
            Assert.DoesNotContain("Waveform", props);
            Assert.DoesNotContain("Samples", props);
            Assert.DoesNotContain("Key", props);
            Assert.DoesNotContain("Secret", props);

            // No byte[] property anywhere (would be raw audio).
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                p => p.PropertyType == typeof(byte[]));
        }
    }
}
