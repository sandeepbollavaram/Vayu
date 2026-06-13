using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class VoskModelCatalogTests
{
    [Fact]
    public void Catalog_HasSmallEnglishModel()
    {
        Assert.NotNull(VoskModelCatalog.TryGet("small-en-us"));
    }

    [Fact]
    public void Recommended_IsSmallEnUs()
    {
        Assert.Equal("small-en-us", VoskModelCatalog.Recommended.Id);
    }

    [Fact]
    public void AllEntries_HaveAllowlistedHttpsZips_AndPositiveSizes()
    {
        Assert.All(VoskModelCatalog.All, m =>
        {
            Assert.True(VoskModelCatalog.IsAllowedDownloadUrl(m.DownloadUrl), m.Id);
            Assert.EndsWith(".zip", m.ArchiveFileName, StringComparison.Ordinal);
            Assert.True(m.ApproxSizeMb > 0);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip")] // not https
    [InlineData("https://evil.example.com/vosk/models/vosk-model-small-en-us-0.15.zip")] // wrong host
    [InlineData("https://alphacephei.com/elsewhere/vosk-model-small-en-us-0.15.zip")]    // wrong path
    public void IsAllowedDownloadUrl_RejectsUntrustedSources(string? url)
    {
        Assert.False(VoskModelCatalog.IsAllowedDownloadUrl(url));
    }

    [Fact]
    public void TryGet_Unknown_ReturnsNull()
    {
        Assert.Null(VoskModelCatalog.TryGet("large-fr"));
    }
}
