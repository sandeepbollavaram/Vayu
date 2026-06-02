using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class WhisperModelCatalogTests
{
    [Fact]
    public void Catalog_HasTinyBaseSmall_English()
    {
        Assert.NotNull(WhisperModelCatalog.TryGet("tiny.en"));
        Assert.NotNull(WhisperModelCatalog.TryGet("base.en"));
        Assert.NotNull(WhisperModelCatalog.TryGet("small.en"));
    }

    [Fact]
    public void Recommended_IsBaseEn()
    {
        Assert.Equal("base.en", WhisperModelCatalog.Recommended.Id);
    }

    [Fact]
    public void AllEntries_HaveAllowlistedHttpsUrls_AndPositiveSizes()
    {
        Assert.All(WhisperModelCatalog.All, m =>
        {
            Assert.True(WhisperModelCatalog.IsAllowedDownloadUrl(m.DownloadUrl), m.Id);
            Assert.StartsWith("ggml-", m.FileName);
            Assert.True(m.ApproxSizeMb > 0);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://huggingface.co/ggerganov/whisper.cpp/resolve/main/x.bin")] // not https
    [InlineData("https://evil.example.com/ggml-base.en.bin")]                      // wrong host
    [InlineData("https://huggingface.co/someone/else/resolve/main/x.bin")]         // wrong path
    public void IsAllowedDownloadUrl_RejectsUntrustedSources(string? url)
    {
        Assert.False(WhisperModelCatalog.IsAllowedDownloadUrl(url));
    }

    [Fact]
    public void TryGet_Unknown_ReturnsNull()
    {
        Assert.Null(WhisperModelCatalog.TryGet("medium.fr"));
    }
}
