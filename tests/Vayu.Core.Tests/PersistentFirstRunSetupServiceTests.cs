using System.Collections.Concurrent;

using Vayu.Core;
using Vayu.Core.Setup;

namespace Vayu.Core.Tests;

public class PersistentFirstRunSetupServiceTests
{
    [Fact]
    public async Task FreshStore_ReturnsEmpty_NotCompleted()
    {
        var store = new FakeStore();
        var svc = NewService(store);

        var state = await svc.GetStateAsync();

        Assert.False(state.Completed);
        Assert.False(state.Skipped);
    }

    [Fact]
    public async Task Complete_Persists_AcrossNewServiceInstances()
    {
        var store = new FakeStore();
        await NewService(store).CompleteAsync();

        // A brand-new service over the same store sees the completed state.
        var reloaded = await NewService(store).GetStateAsync();

        Assert.True(reloaded.Completed);
        Assert.NotNull(reloaded.CompletedAtUtc);
    }

    [Fact]
    public async Task Skip_Persists_AsCompletedAndSkipped()
    {
        var store = new FakeStore();
        await NewService(store).SkipAsync();

        var reloaded = await NewService(store).GetStateAsync();

        Assert.True(reloaded.Completed); // routes to Command Center
        Assert.True(reloaded.Skipped);
    }

    [Fact]
    public async Task Update_Persists_StoragePaths_AndWhisperModel()
    {
        var store = new FakeStore();
        var svc = NewService(store);
        var paths = VayuStoragePaths.Default();

        var current = await svc.GetStateAsync();
        await svc.UpdateAsync(current with
        {
            StoragePaths = paths,
            WhisperModelPath = @"C:\models\ggml-base.en.bin",
        });

        var reloaded = await NewService(store).GetStateAsync();
        Assert.Equal(paths.WorkspaceRoot, reloaded.StoragePaths?.WorkspaceRoot);
        Assert.Equal(@"C:\models\ggml-base.en.bin", reloaded.WhisperModelPath);
    }

    [Fact]
    public async Task PersistedState_NeverContainsApiKey()
    {
        var store = new FakeStore();
        var svc = NewService(store);

        var current = await svc.GetStateAsync();
        // Even if a key were ever present in memory, only the boolean is modelled.
        await svc.UpdateAsync(current with { GeminiKeyConfigured = true });

        var raw = await store.GetAsync(PersistentFirstRunSetupService.StateKey);
        Assert.NotNull(raw);
        Assert.Contains("GeminiKeyConfigured", raw);
        // No place to even put a key: the state has no key field. Guard the JSON.
        Assert.DoesNotContain("AIza", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", raw.Replace("\"GeminiKeyConfigured\"", ""), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_ClearsCompletion()
    {
        var store = new FakeStore();
        var svc = NewService(store);
        await svc.CompleteAsync();

        await svc.ResetAsync();

        var reloaded = await NewService(store).GetStateAsync();
        Assert.False(reloaded.Completed);
    }

    [Fact]
    public async Task CorruptJson_FallsBackToEmpty()
    {
        var store = new FakeStore();
        await store.SetAsync(PersistentFirstRunSetupService.StateKey, "{ not json");

        var state = await NewService(store).GetStateAsync();

        Assert.False(state.Completed);
    }

    private static PersistentFirstRunSetupService NewService(FakeStore store)
        => new(store.GetAsync, store.SetAsync, new FixedClock(DateTimeOffset.UnixEpoch));

    private sealed class FakeStore
    {
        private readonly ConcurrentDictionary<string, string> _map = new();

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult(_map.TryGetValue(key, out var v) ? v : null);

        public Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            _map[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) { UtcNow = now; }
        public DateTimeOffset UtcNow { get; }
    }
}
