using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class LocalAiReadinessTests
{
    [Fact]
    public void SelectActiveModel_PrefersRecommended_WhenInstalled()
    {
        var active = LocalAiReadiness.SelectActiveModel(new[] { "llama3.2:3b", "gemma3:4b", "gemma3:1b" });

        Assert.Equal("gemma3:4b", active);
    }

    [Fact]
    public void SelectActiveModel_FallsBackToGemma1b_WhenRecommendedMissing()
    {
        var active = LocalAiReadiness.SelectActiveModel(new[] { "gemma3:1b", "llama3.2:3b" });

        Assert.Equal("gemma3:1b", active);
    }

    [Fact]
    public void SelectActiveModel_FallsBackToLlama_WhenOnlyLlamaInstalled()
    {
        var active = LocalAiReadiness.SelectActiveModel(new[] { "llama3.2:3b" });

        Assert.Equal("llama3.2:3b", active);
    }

    [Fact]
    public void SelectActiveModel_IgnoresUnknownTags()
    {
        var active = LocalAiReadiness.SelectActiveModel(new[] { "mistral:7b", "phi3:mini" });

        Assert.Null(active);
    }

    [Fact]
    public void SelectActiveModel_CaseInsensitive()
    {
        var active = LocalAiReadiness.SelectActiveModel(new[] { "GEMMA3:4B" });

        Assert.Equal("gemma3:4b", active);
    }

    [Theory]
    [InlineData(null)]
    public void SelectActiveModel_NullList_ReturnsNull(IEnumerable<string>? tags)
    {
        Assert.Null(LocalAiReadiness.SelectActiveModel(tags));
    }

    [Fact]
    public void SelectActiveModel_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Null(LocalAiReadiness.SelectActiveModel(Array.Empty<string>()));
        Assert.Null(LocalAiReadiness.SelectActiveModel(new[] { "", "   " }));
    }

    [Fact]
    public void CanEnablePlanner_RequiresReachable_AndActiveModel()
    {
        Assert.True(LocalAiReadiness.CanEnablePlanner(true, "gemma3:4b"));
        Assert.False(LocalAiReadiness.CanEnablePlanner(false, "gemma3:4b"));
        Assert.False(LocalAiReadiness.CanEnablePlanner(true, null));
        Assert.False(LocalAiReadiness.CanEnablePlanner(true, ""));
        Assert.False(LocalAiReadiness.CanEnablePlanner(false, null));
    }

    [Fact]
    public void DescribeReadiness_NoServer_AsksForOllama()
    {
        Assert.Contains("Ollama", LocalAiReadiness.DescribeReadiness(false, null));
    }

    [Fact]
    public void DescribeReadiness_ReachableNoModel_AsksForDownload()
    {
        Assert.Contains("Download", LocalAiReadiness.DescribeReadiness(true, null));
    }

    [Fact]
    public void DescribeReadiness_Ready_NamesActiveModel()
    {
        var msg = LocalAiReadiness.DescribeReadiness(true, "gemma3:1b");

        Assert.Contains("ready", msg);
        Assert.Contains("gemma3:1b", msg);
    }
}
