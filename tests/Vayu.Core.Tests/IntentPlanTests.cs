namespace Vayu.Core.Tests;

public class IntentPlanTests
{
    [Fact]
    public void Construction_WithRequiredFields_SetsDefaults()
    {
        var plan = new IntentPlan
        {
            Intent = "app.launch",
            Risk = RiskLevel.L1,
        };

        Assert.Equal("app.launch", plan.Intent);
        Assert.Equal(RiskLevel.L1, plan.Risk);
        Assert.Empty(plan.Args);
        Assert.Null(plan.Confidence);
        Assert.Null(plan.PlanSource);
        Assert.NotEqual(Guid.Empty, plan.CorrelationId);
    }

    [Fact]
    public void Args_AreReadOnly_OnceSet()
    {
        var plan = new IntentPlan
        {
            Intent = "app.launch",
            Risk = RiskLevel.L1,
            Args = new Dictionary<string, string> { ["app"] = "vscode" },
        };

        Assert.Single(plan.Args);
        Assert.Equal("vscode", plan.Args["app"]);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(plan.Args);
    }

    [Fact]
    public void RecordEquality_IgnoresReferenceIdentity_OnSameValues()
    {
        var corr = Guid.NewGuid();
        var a = new IntentPlan { Intent = "x", Risk = RiskLevel.L1, CorrelationId = corr };
        var b = new IntentPlan { Intent = "x", Risk = RiskLevel.L1, CorrelationId = corr };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Construction_WithFullMetadata_RoundTripsValues()
    {
        var plan = new IntentPlan
        {
            Intent = "gmail.draft",
            Risk = RiskLevel.L4,
            Args = new Dictionary<string, string> { ["to"] = "rahul@example.com" },
            Confidence = 0.87,
            PlanSource = "ollama:gemma3:4b",
        };

        Assert.Equal(RiskLevel.L4, plan.Risk);
        Assert.Equal(0.87, plan.Confidence);
        Assert.Equal("ollama:gemma3:4b", plan.PlanSource);
        Assert.Equal("rahul@example.com", plan.Args["to"]);
    }
}
