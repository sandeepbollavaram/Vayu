using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class OllamaModelInfoTests
{
    [Theory]
    [InlineData(null,        "")]
    [InlineData(0L,          "")]
    [InlineData(-5L,         "")]
    [InlineData(512L,        "1 KB")]    // < 1 KiB rounds up via formatter floor for display
    [InlineData(1500L,       "1 KB")]
    [InlineData(1_048_576L,  "1 MB")]
    [InlineData(500_000_000L, "477 MB")]
    [InlineData(3_826_793_677L, "3.6 GB")]
    [InlineData(7_500_000_000L, "7.0 GB")]
    public void FormatSize_ProducesExpectedHumanString(long? sizeBytes, string expected)
    {
        Assert.Equal(expected, OllamaModelInfo.FormatSize(sizeBytes));
    }

    [Fact]
    public void DisplaySize_DerivesFromSizeBytes()
    {
        var info = new OllamaModelInfo("gemma3", "gemma3:4b", 3_826_793_677, true);

        Assert.Equal("3.6 GB", info.DisplaySize);
    }

    [Fact]
    public void DisplaySize_EmptyString_WhenSizeUnknown()
    {
        var info = new OllamaModelInfo("gemma3", "gemma3:4b", null, true);

        Assert.Equal(string.Empty, info.DisplaySize);
    }

    [Fact]
    public void PositionalConstructor_KeepsExistingCallSitesWorking()
    {
        // Older code (M2.2/M2.3) constructs OllamaModelInfo positionally. M2.4 must
        // preserve that — additional fields are init-only with safe defaults.
        var info = new OllamaModelInfo("gemma3", "gemma3:4b", 1_000_000_000, true);

        Assert.Null(info.Digest);
        Assert.Null(info.ModifiedAtUtc);
        Assert.Null(info.Family);
        Assert.Null(info.ParameterSize);
        Assert.Null(info.QuantizationLevel);
    }

    [Fact]
    public void InitOnlyProperties_CanBePopulated()
    {
        var info = new OllamaModelInfo("gemma3", "gemma3:4b", 1_000_000_000, true)
        {
            Digest = "sha256:abc",
            ModifiedAtUtc = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
            Family = "gemma3",
            ParameterSize = "4B",
            QuantizationLevel = "Q4_K_M",
        };

        Assert.Equal("sha256:abc", info.Digest);
        Assert.Equal("4B", info.ParameterSize);
        Assert.Equal("Q4_K_M", info.QuantizationLevel);
    }
}
