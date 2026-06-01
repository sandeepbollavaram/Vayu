using Vayu.Automation.Windows;

namespace Vayu.Automation.Windows.Tests;

public class KnownUriCatalogTests
{
    [Theory]
    [InlineData("spotify:")]
    [InlineData("whatsapp:")]
    [InlineData("spotify://")]
    [InlineData("SPOTIFY:")]
    [InlineData("ms-settings:")]
    public void IsAllowedLaunchUri_AllowsBareVettedSchemes(string target)
    {
        Assert.True(KnownUriCatalog.IsAllowedLaunchUri(target));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAllowedLaunchUri_RejectsNullOrBlank(string? target)
    {
        Assert.False(KnownUriCatalog.IsAllowedLaunchUri(target));
    }

    [Theory]
    // Unknown / unvetted schemes — even legitimate-looking ones — are rejected.
    [InlineData("calc:")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("http://example.com")]
    [InlineData("ms-store:")]
    [InlineData("shell:")]
    public void IsAllowedLaunchUri_RejectsUnvettedSchemes(string target)
    {
        Assert.False(KnownUriCatalog.IsAllowedLaunchUri(target));
    }

    [Theory]
    // A vetted scheme must still be PAYLOAD-FREE. These all carry a payload,
    // query, fragment, whitespace, or chained command and must be rejected so
    // a crafted command can never drive a registered handler with an argument.
    [InlineData("spotify:track/123")]
    [InlineData("spotify://open?uri=evil")]
    [InlineData("spotify: && calc")]
    [InlineData("spotify:\ncalc")]
    [InlineData("spotify:\tcalc")]
    [InlineData("spotify:#frag")]
    [InlineData("spotify")]      // no colon at all
    [InlineData(":spotify")]     // empty scheme
    public void IsAllowedLaunchUri_RejectsPayloadsAndInjection(string target)
    {
        Assert.False(KnownUriCatalog.IsAllowedLaunchUri(target));
    }

    [Fact]
    public void AllowedSchemes_AreLowercase_AndNonEmpty()
    {
        Assert.NotEmpty(KnownUriCatalog.AllowedSchemes);
        Assert.All(KnownUriCatalog.AllowedSchemes, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s));
            Assert.Equal(s.ToLowerInvariant(), s);
        });
    }
}
