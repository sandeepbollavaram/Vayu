using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class LocalAiProviderStatusTests
{
    [Fact]
    public void Unavailable_Returns_SafeDefaults()
    {
        var endpoint = new Uri("http://localhost:11434");

        var status = LocalAiProviderStatus.Unavailable("ollama", endpoint, "Ollama is not running.");

        Assert.Equal("ollama", status.ProviderName);
        Assert.False(status.IsAvailable);
        Assert.False(status.IsRunning);
        Assert.False(status.RecommendedModelPresent);
        Assert.Empty(status.InstalledModels);
        Assert.Equal(endpoint, status.Endpoint);
        Assert.Equal("Ollama is not running.", status.Message);
    }

    [Fact]
    public void Unavailable_Message_DefaultsToNull()
    {
        var status = LocalAiProviderStatus.Unavailable("ollama", new Uri("http://localhost:11434"));

        Assert.Null(status.Message);
    }

    [Fact]
    public void RunningStatus_Represents_HealthyProvider()
    {
        var status = new LocalAiProviderStatus(
            ProviderName: "ollama",
            IsAvailable: true,
            IsRunning: true,
            Endpoint: new Uri("http://localhost:11434"),
            InstalledModels: new[] { "gemma3:4b", "llama3.2:3b" },
            RecommendedModelPresent: true,
            Message: null);

        Assert.True(status.IsAvailable);
        Assert.True(status.IsRunning);
        Assert.True(status.RecommendedModelPresent);
        Assert.Equal(2, status.InstalledModels.Count);
        Assert.Contains("gemma3:4b", status.InstalledModels);
    }

    [Fact]
    public void Status_RecordEquality_IsValueBased()
    {
        var endpoint = new Uri("http://localhost:11434");
        var a = LocalAiProviderStatus.Unavailable("ollama", endpoint);
        var b = LocalAiProviderStatus.Unavailable("ollama", endpoint);

        // Reference-equal InstalledModels (Array.Empty) makes the records value-equal too.
        Assert.Equal(a, b);
    }

    [Fact]
    public void Status_InstalledModels_IsReadOnlyList()
    {
        var status = LocalAiProviderStatus.Unavailable("ollama", new Uri("http://localhost:11434"));

        Assert.IsAssignableFrom<IReadOnlyList<string>>(status.InstalledModels);
    }
}
