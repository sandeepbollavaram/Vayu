using Vayu.AI.Online;

namespace Vayu.AI.Online.Tests;

public class OnlineAiOptionsTests
{
    [Fact]
    public void OnlineAi_DisabledByDefault()
    {
        Assert.False(new OnlineAiOptions().EnableOnlineAi);
    }

    [Fact]
    public void HybridMode_DisabledByDefault()
    {
        Assert.False(new OnlineAiOptions().EnableHybridMode);
    }

    [Fact]
    public void Consent_RequiredByDefault()
    {
        Assert.True(new OnlineAiOptions().RequireConsentBeforeCloudCall);
    }

    [Fact]
    public void DefaultProviderId_EmptyByDefault()
    {
        Assert.True(string.IsNullOrEmpty(new OnlineAiOptions().DefaultProviderId));
    }

    [Fact]
    public void Timeout_And_MaxPrompt_AreSafeDefaults()
    {
        var o = new OnlineAiOptions();

        Assert.InRange(o.TimeoutSeconds, 1, 120);
        Assert.InRange(o.MaxPromptChars, 1000, 100_000);
    }
}
