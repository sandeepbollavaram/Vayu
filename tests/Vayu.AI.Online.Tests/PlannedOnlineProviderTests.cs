using System.Reflection;

using Vayu.AI.Online;
using Vayu.Core;

namespace Vayu.AI.Online.Tests;

public class PlannedOnlineProviderTests
{
    [Fact]
    public void Shell_ProviderId_FromConstructor()
    {
        var shell = new PlannedOnlineProvider("openai", "OpenAI");

        Assert.Equal("openai", shell.ProviderId);
        Assert.Equal("OpenAI", shell.DisplayName);
    }

    [Fact]
    public void Shell_FromDescriptor_UsesCatalogNames()
    {
        var descriptor = OnlineProviderCatalog.FindById("claude")!;
        var shell = new PlannedOnlineProvider(descriptor);

        Assert.Equal("claude", shell.ProviderId);
        Assert.Equal(descriptor.DisplayName, shell.DisplayName);
    }

    [Fact]
    public async Task Shell_KeyStatus_NotConfigured_NoValue()
    {
        var shell = new PlannedOnlineProvider("openai");

        var status = await shell.GetKeyStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.NotConfigured, status.Source);
    }

    [Fact]
    public async Task Shell_PlanAsync_ReturnsSafeNotSupported()
    {
        var shell = new PlannedOnlineProvider("deepseek", "DeepSeek");

        var result = await shell.PlanAsync(Request("open notepad"), CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Null(result.IntentPlan);
        Assert.Contains("not implemented yet", result.ErrorMessage);
        Assert.Equal("deepseek", result.ProviderId);
    }

    [Fact]
    public async Task Shell_PlanAsync_SafeEvenWithoutConsent()
    {
        // A shell never calls out, so even a Cancel decision just yields the safe failure.
        var shell = new PlannedOnlineProvider("kimi");

        var result = await shell.PlanAsync(Request("open notepad"), CloudConsentDecision.Cancel);

        Assert.False(result.Success);
        Assert.Null(result.IntentPlan);
    }

    [Fact]
    public void Shell_Throws_OnBlankProviderId()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PlannedOnlineProvider("  "));
    }

    [Fact]
    public void Shell_HasNoSecretProperty()
    {
        var propNames = typeof(PlannedOnlineProvider)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Key", propNames);
        Assert.DoesNotContain("ApiKey", propNames);
        Assert.DoesNotContain("Secret", propNames);
        Assert.DoesNotContain("Token", propNames);
    }

    [Fact]
    public void BuildAll_CoversEveryNonGeminiProvider()
    {
        var shells = PlannedOnlineProviders.BuildAll();

        Assert.Equal(OnlineProviderCatalog.ListAll().Count - 1, shells.Count);
        Assert.DoesNotContain(shells, s => s.ProviderId == "gemini");
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("claude")]
    [InlineData("deepseek")]
    [InlineData("kimi")]
    [InlineData("openrouter")]
    [InlineData("custom-openai-compatible")]
    public void HasShell_TrueForExpectedProviders(string id)
    {
        Assert.True(PlannedOnlineProviders.HasShell(id));
    }

    [Fact]
    public void HasShell_FalseForGemini_AndCaseInsensitive()
    {
        Assert.False(PlannedOnlineProviders.HasShell("gemini"));
        Assert.True(PlannedOnlineProviders.HasShell("OpenAI"));
    }

    private static CommandRequest Request(string text) => new() { Text = text, Source = "text" };
}
