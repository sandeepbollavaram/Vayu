using System.Reflection;

using Vayu.AI.Online;

namespace Vayu.AI.Online.Tests;

public class ProviderRegistryViewModelTests
{
    [Fact]
    public void BuildCards_CountMatchesCatalog()
    {
        var cards = ProviderRegistryViewModel.BuildCards(geminiKeyConfigured: false);

        Assert.Equal(OnlineProviderCatalog.ListAll().Count, cards.Count);
    }

    [Fact]
    public void Gemini_IsAvailableNow()
    {
        var cards = ProviderRegistryViewModel.BuildCards(geminiKeyConfigured: false);

        var gemini = cards.Single(c => c.ProviderId == "gemini");
        Assert.True(gemini.IsAvailableNow);
        Assert.Contains("Available now", gemini.StatusLabel);
    }

    [Fact]
    public void NonGeminiProviders_AreMarkedPlanned_AndDisabled()
    {
        var cards = ProviderRegistryViewModel.BuildCards(geminiKeyConfigured: true);

        foreach (var card in cards.Where(c => c.ProviderId != "gemini"))
        {
            Assert.False(card.IsAvailableNow);
            Assert.Equal("Planned", card.StatusLabel);
            Assert.False(card.IsActionEnabled);
            Assert.Contains("M3.7", card.ActionLabel);
        }
    }

    [Fact]
    public void Gemini_ConfiguredStatus_MapsFromKeyFlag()
    {
        var notConfigured = ProviderRegistryViewModel.BuildCards(geminiKeyConfigured: false).Single(c => c.ProviderId == "gemini");
        var configured = ProviderRegistryViewModel.BuildCards(geminiKeyConfigured: true).Single(c => c.ProviderId == "gemini");

        Assert.False(notConfigured.IsConfigured);
        Assert.Contains("Not configured", notConfigured.StatusLabel);
        Assert.True(configured.IsConfigured);
        Assert.Contains("Configured", configured.StatusLabel);
    }

    [Fact]
    public void Cards_CarryCapabilityLabels()
    {
        var gemini = ProviderRegistryViewModel.BuildCards(false).Single(c => c.ProviderId == "gemini");

        Assert.Contains("Chat", gemini.CapabilityLabels);
        Assert.Contains("JSON mode", gemini.CapabilityLabels);
    }

    [Fact]
    public void OpenRouter_KindLabel_IsRouter()
    {
        var openrouter = ProviderRegistryViewModel.BuildCards(false).Single(c => c.ProviderId == "openrouter");

        Assert.Equal("Router", openrouter.KindLabel);
    }

    [Fact]
    public void Custom_KindLabel_IsOpenAiCompatible()
    {
        var custom = ProviderRegistryViewModel.BuildCards(false).Single(c => c.ProviderId == "custom-openai-compatible");

        Assert.Equal("Custom OpenAI-compatible", custom.KindLabel);
    }

    [Fact]
    public void NoCard_ExposesSecretField()
    {
        var propNames = typeof(ProviderCardViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Key", propNames);
        Assert.DoesNotContain("ApiKey", propNames);
        Assert.DoesNotContain("Secret", propNames);
        Assert.DoesNotContain("Token", propNames);
    }

    [Fact]
    public void ExactlyOneProvider_IsAvailableNow()
    {
        var cards = ProviderRegistryViewModel.BuildCards(true);

        Assert.Single(cards, c => c.IsAvailableNow);
    }
}
