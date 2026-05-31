using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class OllamaProviderOptionsTests
{
    [Fact]
    public void Default_Endpoint_IsOllamaLocalhost()
    {
        var options = new OllamaProviderOptions();

        Assert.Equal(new Uri("http://localhost:11434"), options.Endpoint);
    }

    [Fact]
    public void Default_ExecutableName_IsOllama()
    {
        Assert.Equal("ollama", new OllamaProviderOptions().ExecutableName);
    }

    [Fact]
    public void Default_WingetPackageId_IsOllamaOllama()
    {
        Assert.Equal("Ollama.Ollama", new OllamaProviderOptions().WingetPackageId);
    }

    [Fact]
    public void Default_ProbeTimeout_IsBounded()
    {
        var timeout = new OllamaProviderOptions().ProbeTimeoutSeconds;

        Assert.InRange(timeout, 1, 30);
        Assert.Equal(5, timeout);
    }

    [Fact]
    public void Record_Supports_WithExpression()
    {
        var custom = new OllamaProviderOptions() with
        {
            Endpoint = new Uri("http://localhost:55555"),
            ExecutableName = "vayu-test-ollama",
            WingetPackageId = "Test.Package",
            ProbeTimeoutSeconds = 2,
        };

        Assert.Equal("vayu-test-ollama", custom.ExecutableName);
        Assert.Equal("Test.Package", custom.WingetPackageId);
        Assert.Equal(2, custom.ProbeTimeoutSeconds);
    }
}
