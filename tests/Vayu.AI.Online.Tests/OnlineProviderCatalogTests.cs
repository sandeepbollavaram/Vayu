using Vayu.AI.Online;

namespace Vayu.AI.Online.Tests;

public class OnlineProviderCatalogTests
{
    [Theory]
    [InlineData("gemini")]
    [InlineData("openai")]
    [InlineData("claude")]
    [InlineData("deepseek")]
    [InlineData("kimi")]
    [InlineData("openrouter")]
    [InlineData("custom-openai-compatible")]
    public void Catalog_ContainsExpectedProviders(string id)
    {
        Assert.NotNull(OnlineProviderCatalog.FindById(id));
    }

    [Fact]
    public void FindById_IsCaseInsensitive()
    {
        Assert.NotNull(OnlineProviderCatalog.FindById("GEMINI"));
        Assert.NotNull(OnlineProviderCatalog.FindById("  Gemini  "));
    }

    [Fact]
    public void FindById_UnknownReturnsNull()
    {
        Assert.Null(OnlineProviderCatalog.FindById("definitely-not-a-provider"));
    }

    [Fact]
    public void NoProvider_EnabledByDefault()
    {
        Assert.All(OnlineProviderCatalog.ListAll(), p => Assert.False(p.IsEnabledByDefault));
    }

    [Fact]
    public void RecommendedFirstProvider_IsGemini()
    {
        Assert.Equal("gemini", OnlineProviderCatalog.RecommendedFirstProvider.ProviderId);
    }

    [Fact]
    public void Catalog_HasNoDuplicateIds()
    {
        var ids = OnlineProviderCatalog.ListAll().Select(p => p.ProviderId.ToLowerInvariant()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void OpenRouter_IsRouterKind_AndCustom_IsOpenAiCompatible()
    {
        Assert.Equal(OnlineProviderKind.Router, OnlineProviderCatalog.FindById("openrouter")!.Kind);
        Assert.Equal(OnlineProviderKind.CustomOpenAiCompatible, OnlineProviderCatalog.FindById("custom-openai-compatible")!.Kind);
    }
}
