using Vayu.Core;
using Vayu.Core.Setup;

namespace Vayu.Core.Tests;

public class InMemoryFirstRunSetupServiceTests
{
    [Fact]
    public async Task GetStateAsync_BeforeAnyAction_ReturnsEmpty()
    {
        var svc = NewService();

        var state = await svc.GetStateAsync();

        Assert.False(state.Completed);
        Assert.Equal(FirstRunSetupMode.OfflineOnly, state.Mode);
        Assert.Null(state.CompletedAtUtc);
    }

    [Fact]
    public async Task StartAsync_RecordsMode_AndClearsCompletion()
    {
        var svc = NewService();
        await svc.CompleteAsync();

        var state = await svc.StartAsync(FirstRunSetupMode.Hybrid);

        Assert.Equal(FirstRunSetupMode.Hybrid, state.Mode);
        Assert.False(state.Completed);
        Assert.Null(state.CompletedAtUtc);
    }

    [Fact]
    public async Task CompleteAsync_SetsCompletionTimestamp()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
        var svc = new InMemoryFirstRunSetupService(clock);

        var state = await svc.CompleteAsync();

        Assert.True(state.Completed);
        Assert.Equal(clock.UtcNow, state.CompletedAtUtc);
    }

    [Fact]
    public async Task ResetAsync_RestoresEmptyState()
    {
        var svc = NewService();
        await svc.StartAsync(FirstRunSetupMode.OnlineOnly);
        await svc.CompleteAsync();

        await svc.ResetAsync();

        var state = await svc.GetStateAsync();
        Assert.False(state.Completed);
        Assert.Equal(FirstRunSetupMode.OfflineOnly, state.Mode);
        Assert.Null(state.CompletedAtUtc);
    }

    [Fact]
    public async Task UpdateDetectionAsync_StoresDetectionSnapshot()
    {
        var svc = NewService();

        var state = await svc.UpdateDetectionAsync(
            ollamaDetected: true,
            endpoint: "http://localhost:11434",
            selectedLocalModel: "gemma3:4b",
            localModelInstalled: true);

        Assert.True(state.OllamaDetected);
        Assert.Equal("http://localhost:11434", state.OllamaEndpoint);
        Assert.Equal("gemma3:4b", state.SelectedLocalModel);
        Assert.True(state.LocalModelInstalled);
    }

    [Fact]
    public void Constructor_Throws_OnNullClock()
    {
        Assert.Throws<ArgumentNullException>(() => new InMemoryFirstRunSetupService(null!));
    }

    private static InMemoryFirstRunSetupService NewService()
        => new(new FixedClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) { UtcNow = now; }
        public DateTimeOffset UtcNow { get; }
    }
}
