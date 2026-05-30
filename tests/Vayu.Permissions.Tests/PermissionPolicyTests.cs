using Vayu.Core;

namespace Vayu.Permissions.Tests;

public class PermissionPolicyTests
{
    [Fact]
    public void Default_MatchesDocumentedThresholds()
    {
        var policy = PermissionPolicy.Default;

        Assert.Equal(RiskLevel.L3, policy.AlwaysConfirmAtOrAbove);
        Assert.Equal(RiskLevel.L6, policy.DisabledAtOrAbove);
    }

    [Fact]
    public void Default_IsShared()
    {
        Assert.Same(PermissionPolicy.Default, PermissionPolicy.Default);
    }

    [Fact]
    public void Record_Supports_WithExpression()
    {
        var custom = PermissionPolicy.Default with
        {
            AlwaysConfirmAtOrAbove = RiskLevel.L2,
            DisabledAtOrAbove = RiskLevel.L5,
        };

        Assert.Equal(RiskLevel.L2, custom.AlwaysConfirmAtOrAbove);
        Assert.Equal(RiskLevel.L5, custom.DisabledAtOrAbove);
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = new PermissionPolicy { AlwaysConfirmAtOrAbove = RiskLevel.L2, DisabledAtOrAbove = RiskLevel.L5 };
        var b = new PermissionPolicy { AlwaysConfirmAtOrAbove = RiskLevel.L2, DisabledAtOrAbove = RiskLevel.L5 };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
