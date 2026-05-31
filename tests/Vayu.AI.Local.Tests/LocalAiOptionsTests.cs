using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class LocalAiOptionsTests
{
    [Fact]
    public void Default_Endpoint_IsOllamaLocalhost()
    {
        var options = new LocalAiOptions();

        Assert.Equal(new Uri("http://localhost:11434"), options.Endpoint);
    }

    [Fact]
    public void Default_Model_IsGemma3_4b()
    {
        var options = new LocalAiOptions();

        Assert.Equal("gemma3:4b", options.DefaultModel);
    }

    [Fact]
    public void Default_TimeoutSeconds_IsSafelyShort()
    {
        var options = new LocalAiOptions();

        Assert.InRange(options.TimeoutSeconds, 1, 300);
        Assert.Equal(30, options.TimeoutSeconds);
    }

    [Fact]
    public void Default_EnableLocalAi_IsFalse_UntilUserOptsIn()
    {
        var options = new LocalAiOptions();

        Assert.False(options.EnableLocalAi);
    }

    [Fact]
    public void Record_Supports_WithExpression()
    {
        var options = new LocalAiOptions();

        var custom = options with
        {
            Endpoint = new Uri("http://localhost:55555"),
            DefaultModel = "gemma3:1b",
            TimeoutSeconds = 10,
            EnableLocalAi = true,
        };

        Assert.Equal(new Uri("http://localhost:55555"), custom.Endpoint);
        Assert.Equal("gemma3:1b", custom.DefaultModel);
        Assert.Equal(10, custom.TimeoutSeconds);
        Assert.True(custom.EnableLocalAi);
    }
}
