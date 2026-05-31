using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class LocalModelCatalogTests
{
    [Theory]
    [InlineData("gemma3:4b")]
    [InlineData("gemma3:1b")]
    [InlineData("llama3.2:3b")]
    public void All_ContainsCuratedModel(string tag)
    {
        Assert.Contains(LocalModelCatalog.All, m => m.ModelTag == tag);
    }

    [Fact]
    public void Recommended_IsGemma3_4b()
    {
        Assert.Equal("gemma3:4b", LocalModelCatalog.Recommended.ModelTag);
        Assert.True(LocalModelCatalog.Recommended.IsRecommended);
    }

    [Fact]
    public void Exactly_OneModel_IsRecommended()
    {
        var recommendedCount = LocalModelCatalog.All.Count(m => m.IsRecommended);

        Assert.Equal(1, recommendedCount);
    }

    [Fact]
    public void Recommended_IsFirst_InDisplayOrder()
    {
        Assert.True(LocalModelCatalog.All[0].IsRecommended);
    }

    [Theory]
    [InlineData("gemma3:4b",  "gemma3:4b")]
    [InlineData("GEMMA3:4B",  "gemma3:4b")]
    [InlineData("Gemma3:4b",  "gemma3:4b")]
    [InlineData("llama3.2:3b","llama3.2:3b")]
    public void FindByTag_IsCaseInsensitive(string query, string expectedTag)
    {
        var found = LocalModelCatalog.FindByTag(query);

        Assert.NotNull(found);
        Assert.Equal(expectedTag, found!.ModelTag);
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("gpt-4")]
    [InlineData("claude-3-opus")]
    public void FindByTag_Unknown_ReturnsNull(string query)
    {
        Assert.Null(LocalModelCatalog.FindByTag(query));
    }

    [Fact]
    public void FindByTag_Throws_OnNullOrWhitespace()
    {
        Assert.ThrowsAny<ArgumentException>(() => LocalModelCatalog.FindByTag(null!));
        Assert.ThrowsAny<ArgumentException>(() => LocalModelCatalog.FindByTag(""));
        Assert.ThrowsAny<ArgumentException>(() => LocalModelCatalog.FindByTag("   "));
    }

    [Fact]
    public void ListAll_Equals_All()
    {
        Assert.Same(LocalModelCatalog.All, LocalModelCatalog.ListAll());
    }

    [Fact]
    public void All_Entries_HaveNonEmptyMetadata()
    {
        foreach (var model in LocalModelCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(model.ModelTag));
            Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(model.Provider));
            Assert.False(string.IsNullOrWhiteSpace(model.Description));
            Assert.False(string.IsNullOrWhiteSpace(model.RecommendedUse));
            Assert.False(string.IsNullOrWhiteSpace(model.HardwareNote));
        }
    }
}
