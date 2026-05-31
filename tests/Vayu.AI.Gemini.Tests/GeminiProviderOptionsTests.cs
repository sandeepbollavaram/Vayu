using System.Reflection;

using Vayu.AI.Gemini;

namespace Vayu.AI.Gemini.Tests;

public class GeminiProviderOptionsTests
{
    [Fact]
    public void ProviderId_IsGemini()
    {
        Assert.Equal("gemini", new GeminiProviderOptions().ProviderId);
    }

    [Fact]
    public void EnvVar_IsGeminiApiKey()
    {
        Assert.Equal("GEMINI_API_KEY", new GeminiProviderOptions().ApiKeyEnvironmentVariable);
    }

    [Fact]
    public void Timeout_And_MaxPrompt_AreSafe()
    {
        var o = new GeminiProviderOptions();

        Assert.InRange(o.TimeoutSeconds, 1, 120);
        Assert.InRange(o.MaxPromptChars, 1000, 100_000);
    }

    [Fact]
    public void Options_HaveNoApiKeyField()
    {
        var propNames = typeof(GeminiProviderOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("ApiKey", propNames);
        Assert.DoesNotContain("Key", propNames);
        Assert.DoesNotContain("Secret", propNames);
        // Only the env-var *name* is present, never a value.
        Assert.Contains("ApiKeyEnvironmentVariable", propNames);
    }

    [Fact]
    public void Endpoint_IsGoogleGenerativeLanguage()
    {
        Assert.Contains("generativelanguage.googleapis.com", new GeminiProviderOptions().Endpoint.ToString());
    }
}
