namespace Vayu.Core.Tests;

public class RiskLevelTests
{
    [Fact]
    public void Levels_AreStableNumericValues()
    {
        Assert.Equal(0, (int)RiskLevel.L0);
        Assert.Equal(1, (int)RiskLevel.L1);
        Assert.Equal(2, (int)RiskLevel.L2);
        Assert.Equal(3, (int)RiskLevel.L3);
        Assert.Equal(4, (int)RiskLevel.L4);
        Assert.Equal(5, (int)RiskLevel.L5);
        Assert.Equal(6, (int)RiskLevel.L6);
    }

    [Fact]
    public void Levels_AreOrdered_LowestToHighest()
    {
        Assert.True(RiskLevel.L0 < RiskLevel.L1);
        Assert.True(RiskLevel.L1 < RiskLevel.L2);
        Assert.True(RiskLevel.L2 < RiskLevel.L3);
        Assert.True(RiskLevel.L3 < RiskLevel.L4);
        Assert.True(RiskLevel.L4 < RiskLevel.L5);
        Assert.True(RiskLevel.L5 < RiskLevel.L6);
    }

    [Theory]
    [InlineData(RiskLevel.L0, false)]
    [InlineData(RiskLevel.L1, false)]
    [InlineData(RiskLevel.L2, false)]
    [InlineData(RiskLevel.L3, true)]
    [InlineData(RiskLevel.L4, true)]
    [InlineData(RiskLevel.L5, true)]
    [InlineData(RiskLevel.L6, true)]
    public void Levels_AtOrAboveL3_AreConsideredRisky(RiskLevel level, bool isRisky)
    {
        // Convention used throughout Vayu: L3+ requires confirmation by default.
        Assert.Equal(isRisky, level >= RiskLevel.L3);
    }

    [Fact]
    public void L6_IsHighest()
    {
        var max = Enum.GetValues<RiskLevel>().Max();
        Assert.Equal(RiskLevel.L6, max);
    }
}
