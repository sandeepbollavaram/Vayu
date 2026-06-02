using Vayu.Automation.Windows;
using Vayu.Core;

namespace Vayu.Automation.Windows.Tests;

public class AutomationConfirmationTests
{
    [Fact]
    public async Task DenyingService_AlwaysCancels()
    {
        var svc = new DenyingAutomationConfirmationService();
        var request = new AutomationConfirmationRequest(
            new AutomationTarget(AppName: "Notepad"),
            AutomationActionType.TypeText,
            RiskLevel.L3,
            "Type into Notepad.",
            "Vayu will type into this window.",
            Guid.NewGuid(),
            TextPreview: "hello");

        var decision = await svc.RequestAsync(request);

        // Fail-closed: with no real UI yet, automation cannot run.
        Assert.Equal(AutomationConfirmationDecision.Cancel, decision);
    }

    [Fact]
    public void FromPlan_CarriesPlanFields_AndWarning()
    {
        var policy = new AutomationSafetyPolicy();
        var plan = policy.Evaluate(
            AutomationActionType.TypeText,
            new AutomationTarget(AppName: "Notepad", WindowTitle: "Untitled - Notepad"),
            "hello",
            Guid.NewGuid());

        var request = AutomationConfirmationRequest.FromPlan(plan, "Vayu will type into this window.");

        Assert.Equal(plan.ActionType, request.ActionType);
        Assert.Equal(plan.RiskLevel, request.RiskLevel);
        Assert.Equal(plan.CorrelationId, request.CorrelationId);
        Assert.Equal("hello", request.TextPreview);
        Assert.Contains("type", request.SafetyWarning, StringComparison.OrdinalIgnoreCase);
    }
}
