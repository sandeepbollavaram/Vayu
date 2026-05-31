using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class OllamaLocalAiProviderTests
{
    [Fact]
    public async Task GetStatusAsync_HealthyRuntime_MapsToLocalProviderStatus()
    {
        var runtime = new FakeRuntime
        {
            Installed = true,
            Reachable = true,
            Models = new[]
            {
                new OllamaModelInfo("gemma3", "gemma3:4b", 3_826_793_677, true),
                new OllamaModelInfo("gemma3", "gemma3:1b",   815_000_000, true),
            },
        };
        var provider = new OllamaLocalAiProvider(runtime);

        var status = await provider.GetStatusAsync();

        Assert.Equal("ollama", status.ProviderName);
        Assert.True(status.IsAvailable);
        Assert.True(status.IsRunning);
        Assert.True(status.RecommendedModelPresent);
        Assert.Equal(2, status.InstalledModels.Count);
        Assert.Contains("gemma3:4b", status.InstalledModels);
        Assert.Equal(new Uri("http://localhost:11434"), status.Endpoint);
        Assert.Null(status.Message);
    }

    [Fact]
    public async Task GetStatusAsync_InstalledButNotRunning_ReportsHelpfulMessage()
    {
        var runtime = new FakeRuntime { Installed = true, Reachable = false };
        var provider = new OllamaLocalAiProvider(runtime);

        var status = await provider.GetStatusAsync();

        Assert.True(status.IsAvailable);
        Assert.False(status.IsRunning);
        Assert.False(status.RecommendedModelPresent);
        Assert.Empty(status.InstalledModels);
        Assert.NotNull(status.Message);
        Assert.Contains("not running", status.Message);
    }

    [Fact]
    public async Task GetStatusAsync_Unavailable_ReportsNotDetected()
    {
        var runtime = new FakeRuntime { Installed = false, Reachable = false };
        var provider = new OllamaLocalAiProvider(runtime);

        var status = await provider.GetStatusAsync();

        Assert.False(status.IsAvailable);
        Assert.False(status.IsRunning);
        Assert.False(status.RecommendedModelPresent);
        Assert.Empty(status.InstalledModels);
        Assert.Contains("not detected", status.Message);
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsOnly_CatalogKnownModels()
    {
        var runtime = new FakeRuntime
        {
            Reachable = true,
            Models = new[]
            {
                new OllamaModelInfo("gemma3", "gemma3:4b", 1, true),
                new OllamaModelInfo("mistral", "mistral:7b", 1, true), // not in catalog
                new OllamaModelInfo("llama3.2", "llama3.2:3b", 1, true),
            },
        };
        var provider = new OllamaLocalAiProvider(runtime);

        var curated = await provider.ListModelsAsync();

        Assert.Equal(2, curated.Count);
        Assert.Contains(curated, m => m.ModelTag == "gemma3:4b");
        Assert.Contains(curated, m => m.ModelTag == "llama3.2:3b");
        Assert.DoesNotContain(curated, m => m.ModelTag == "mistral:7b");
    }

    [Fact]
    public async Task ListModelsAsync_Deduplicates_ByTag()
    {
        var runtime = new FakeRuntime
        {
            Reachable = true,
            Models = new[]
            {
                new OllamaModelInfo("gemma3", "gemma3:4b", 1, true),
                new OllamaModelInfo("gemma3", "gemma3:4b", 1, true), // duplicate
            },
        };
        var provider = new OllamaLocalAiProvider(runtime);

        var curated = await provider.ListModelsAsync();

        Assert.Single(curated);
    }

    [Fact]
    public async Task ListModelsAsync_EmptyRuntime_ReturnsEmpty()
    {
        var runtime = new FakeRuntime { Reachable = true };
        var provider = new OllamaLocalAiProvider(runtime);

        Assert.Empty(await provider.ListModelsAsync());
    }

    [Fact]
    public void Constructor_Throws_OnNullRuntime()
    {
        Assert.Throws<ArgumentNullException>(() => new OllamaLocalAiProvider(null!));
    }

    [Fact]
    public void ProviderId_IsStable()
    {
        Assert.Equal("ollama", OllamaLocalAiProvider.ProviderId);
    }

    private sealed class FakeRuntime : IOllamaRuntimeService
    {
        public Uri Endpoint { get; set; } = new Uri("http://localhost:11434");
        public bool Installed { get; set; }
        public bool Reachable { get; set; }
        public IReadOnlyList<OllamaModelInfo> Models { get; set; } = Array.Empty<OllamaModelInfo>();

        public Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default) => Task.FromResult(Installed);

        public Task<bool> IsReachableAsync(CancellationToken cancellationToken = default) => Task.FromResult(Reachable);

        public Task<IReadOnlyList<OllamaModelInfo>> ListLocalModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Models);
    }
}
