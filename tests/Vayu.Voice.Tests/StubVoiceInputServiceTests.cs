using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class StubVoiceInputServiceTests
{
    [Fact]
    public async Task MicStatus_DoesNotClaimRealCapture()
    {
        var svc = new StubVoiceInputService();

        var status = await svc.GetMicrophoneStatusAsync();

        Assert.False(status.PermissionGranted);
        Assert.False(status.CanCapture);
        Assert.Contains("M4.3", status.Message);
    }

    [Fact]
    public async Task StartPushToTalk_ReturnsNotImplemented_NeverFakesTranscript()
    {
        var svc = new StubVoiceInputService();
        var session = VoiceSession.StartPushToTalk(DateTimeOffset.UtcNow);

        var result = await svc.StartPushToTalkAsync(session);

        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.Transcript);
        Assert.Contains("M4.3", result.ErrorMessage);
        Assert.Equal(StubVoiceInputService.ProviderName, result.ProviderName);
    }

    [Fact]
    public async Task Stop_IsSafe()
    {
        var svc = new StubVoiceInputService();

        await svc.StopAsync(); // must not throw
    }

    [Fact]
    public async Task StartPushToTalk_RespectsCancellation()
    {
        var svc = new StubVoiceInputService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.StartPushToTalkAsync(VoiceSession.StartPushToTalk(DateTimeOffset.UtcNow), cts.Token));
    }
}

public class InMemoryVoiceActivitySinkTests
{
    [Fact]
    public async Task Publish_StoresLatest()
    {
        var sink = new InMemoryVoiceActivitySink();
        var session = VoiceSession.StartPushToTalk(DateTimeOffset.UtcNow);
        var ev = VoiceEvent.FromState(session, "listening", DateTimeOffset.UtcNow);

        await sink.PublishAsync(ev);

        Assert.Equal(ev, sink.Latest);
        Assert.Single(sink.Snapshot());
    }

    [Fact]
    public async Task Publish_TrimsToCapacity()
    {
        var sink = new InMemoryVoiceActivitySink(capacity: 2);
        var session = VoiceSession.StartPushToTalk(DateTimeOffset.UtcNow);

        for (var i = 0; i < 5; i++)
        {
            await sink.PublishAsync(VoiceEvent.FromState(session, $"e{i}", DateTimeOffset.UtcNow));
        }

        Assert.Equal(2, sink.Snapshot().Count);
    }

    [Fact]
    public void NewSink_HasNoLatest()
    {
        Assert.Null(new InMemoryVoiceActivitySink().Latest);
    }
}
