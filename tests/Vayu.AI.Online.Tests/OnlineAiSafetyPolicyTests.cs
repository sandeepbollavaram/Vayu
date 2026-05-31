using System.Collections.Immutable;

using Vayu.AI.Online;
using Vayu.Core;

namespace Vayu.AI.Online.Tests;

public class OnlineAiSafetyPolicyTests
{
    [Theory]
    [InlineData("app.open")]
    [InlineData("ui.show_logs")]
    [InlineData("ui.show_settings")]
    [InlineData("unknown")]
    public void AllowedIntents_Pass(string intent)
    {
        Assert.True(OnlineAiSafetyPolicy.IsIntentAllowed(intent));
    }

    [Theory]
    [InlineData("shell.run")]
    [InlineData("file.delete")]
    [InlineData("email.send")]
    [InlineData("ui.click")]
    [InlineData("ui.type")]
    [InlineData("system.admin")]
    public void DisallowedIntents_Fail(string intent)
    {
        Assert.False(OnlineAiSafetyPolicy.IsIntentAllowed(intent));
    }

    [Fact]
    public void Validate_AcceptsAppOpenAtL1()
    {
        var plan = Plan("app.open", RiskLevel.L1);

        Assert.True(OnlineAiSafetyPolicy.Validate(plan, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Validate_RejectsNonAllowlistedIntent()
    {
        var plan = Plan("shell.run", RiskLevel.L1);

        Assert.False(OnlineAiSafetyPolicy.Validate(plan, out var error));
        Assert.Contains("non-allowlisted", error);
    }

    [Fact]
    public void Validate_RejectsRiskAboveCeiling()
    {
        // Even an allowlisted intent must not carry a risk above L1 in M3.
        var plan = Plan("app.open", RiskLevel.L5);

        Assert.False(OnlineAiSafetyPolicy.Validate(plan, out var error));
        Assert.Contains("above the M3 ceiling", error);
    }

    [Fact]
    public void Validate_RejectsNullPlan()
    {
        Assert.False(OnlineAiSafetyPolicy.Validate(null, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void RiskForIntent_IsAssignedByVayu()
    {
        Assert.Equal(RiskLevel.L1, OnlineAiSafetyPolicy.RiskForIntent("app.open"));
        Assert.Equal(RiskLevel.L0, OnlineAiSafetyPolicy.RiskForIntent("ui.show_logs"));
        Assert.Equal(RiskLevel.L0, OnlineAiSafetyPolicy.RiskForIntent("ui.show_settings"));
    }

    private static IntentPlan Plan(string intent, RiskLevel risk) => new()
    {
        Intent = intent,
        Risk = risk,
        Args = ImmutableDictionary<string, string>.Empty,
        Confidence = 0.9,
        PlanSource = "gemini:test",
    };
}
