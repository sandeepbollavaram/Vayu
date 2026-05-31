using System.Reflection;

using Vayu.AI.Online;

namespace Vayu.AI.Online.Tests;

public class CloudConsentRequestTests
{
    [Fact]
    public void Request_CapturesProviderPurposeAndDataSummary()
    {
        var req = new CloudConsentRequest(
            ProviderId: "gemini",
            ProviderDisplayName: "Google Gemini",
            Purpose: "Plan your command",
            DataSummary: "your typed command text",
            EstimatedPromptChars: 42,
            RequiresSensitiveContext: false,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        Assert.Equal("gemini", req.ProviderId);
        Assert.Equal("Plan your command", req.Purpose);
        Assert.Equal("your typed command text", req.DataSummary);
        Assert.False(req.RequiresSensitiveContext);
    }

    [Fact]
    public void Request_HasNoKeyOrRawPromptField()
    {
        var propNames = typeof(CloudConsentRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Key", propNames);
        Assert.DoesNotContain("ApiKey", propNames);
        Assert.DoesNotContain("Secret", propNames);
        Assert.DoesNotContain("Prompt", propNames);   // only EstimatedPromptChars, not the raw prompt
        Assert.Contains("EstimatedPromptChars", propNames);
    }

    [Fact]
    public void ConsentDecision_DefaultsToSafeOrdering()
    {
        // AllowOnce is the only value that should permit a cloud call.
        Assert.Equal(0, (int)CloudConsentDecision.AllowOnce);
        Assert.True(Enum.IsDefined(CloudConsentDecision.UseLocalInstead));
        Assert.True(Enum.IsDefined(CloudConsentDecision.Cancel));
    }
}
