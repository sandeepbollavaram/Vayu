using System.Reflection;

using Vayu.AI.Online;

namespace Vayu.AI.Online.Tests;

public class OnlineProviderKeyStatusTests
{
    [Fact]
    public void NotConfigured_IsSafeDefault()
    {
        var status = OnlineProviderKeyStatus.NotConfigured("gemini");

        Assert.False(status.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.NotConfigured, status.Source);
        Assert.Equal("gemini", status.ProviderId);
    }

    [Fact]
    public void Configured_NamesSourceOnly()
    {
        var status = OnlineProviderKeyStatus.Configured("gemini", OnlineProviderKeySource.WindowsCredentialManager);

        Assert.True(status.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.WindowsCredentialManager, status.Source);
    }

    [Fact]
    public void Configured_RejectsNotConfiguredSource()
    {
        Assert.Throws<ArgumentException>(
            () => OnlineProviderKeyStatus.Configured("gemini", OnlineProviderKeySource.NotConfigured));
    }

    [Fact]
    public void KeyStatus_HasNoPropertyThatCouldHoldAKeyValue()
    {
        // Guard against a future field accidentally carrying the secret. The type
        // must expose only id / configured-flag / source / message.
        var propNames = typeof(OnlineProviderKeyStatus)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Key", propNames);
        Assert.DoesNotContain("ApiKey", propNames);
        Assert.DoesNotContain("Secret", propNames);
        Assert.DoesNotContain("Token", propNames);
        Assert.DoesNotContain("Value", propNames);
    }
}
