using Vayu.Automation.Windows;

namespace Vayu.Automation.Windows.Tests;

public class KnownAppCatalogTests
{
    [Theory]
    [InlineData("chrome")]
    [InlineData("edge")]
    [InlineData("vscode")]
    [InlineData("notepad")]
    [InlineData("terminal")]
    [InlineData("downloads")]
    public void KnownAppIds_Resolve(string appId)
    {
        var info = KnownAppCatalog.TryGet(appId);

        Assert.NotNull(info);
        Assert.Equal(appId, info!.AppId);
    }

    [Theory]
    [InlineData("CHROME")]
    [InlineData("Chrome")]
    [InlineData("  notepad  ")]
    [InlineData("VS Code")]
    public void Lookup_IsCaseAndWhitespaceInsensitive(string input)
    {
        Assert.NotNull(KnownAppCatalog.TryGet(input));
    }

    [Theory]
    [InlineData("vs code",            "vscode")]
    [InlineData("visual studio code", "vscode")]
    [InlineData("windows terminal",   "terminal")]
    [InlineData("wt",                 "terminal")]
    [InlineData("google chrome",      "chrome")]
    [InlineData("microsoft edge",     "edge")]
    public void Aliases_NormalizeToCanonicalId(string alias, string canonicalId)
    {
        var info = KnownAppCatalog.TryGet(alias);

        Assert.NotNull(info);
        Assert.Equal(canonicalId, info!.AppId);
    }

    [Fact]
    public void Downloads_HasFolderLaunchKind_AndEnvVarTarget()
    {
        var info = KnownAppCatalog.TryGet("downloads");

        Assert.NotNull(info);
        Assert.Equal(LaunchKind.Folder, info!.LaunchKind);
        Assert.Contains("%USERPROFILE%", info.LaunchTarget, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("spaceship")]
    [InlineData("explorer")]
    [InlineData("totally-not-an-app")]
    public void Unknown_AppOrAlias_ReturnsNull(string input)
    {
        Assert.Null(KnownAppCatalog.TryGet(input));
    }

    [Fact]
    public void TryGet_Throws_OnNullOrWhitespace()
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        // and ArgumentException for empty/whitespace — both inherit from ArgumentException.
        Assert.ThrowsAny<ArgumentException>(() => KnownAppCatalog.TryGet(null!));
        Assert.ThrowsAny<ArgumentException>(() => KnownAppCatalog.TryGet(""));
        Assert.ThrowsAny<ArgumentException>(() => KnownAppCatalog.TryGet("   "));
    }

    [Fact]
    public void All_AppIds_AreUnique()
    {
        var ids = KnownAppCatalog.All.Select(a => a.AppId).ToList();
        var distinct = ids.Distinct(StringComparer.Ordinal).Count();

        Assert.Equal(ids.Count, distinct);
    }

    [Fact]
    public void All_AppIds_AreLowercase()
    {
        Assert.All(KnownAppCatalog.All, info =>
            Assert.Equal(info.AppId.ToLowerInvariant(), info.AppId));
    }

    [Fact]
    public void All_HasNonEmptyDisplayNames()
    {
        Assert.All(KnownAppCatalog.All, info =>
            Assert.False(string.IsNullOrWhiteSpace(info.DisplayName)));
    }
}
