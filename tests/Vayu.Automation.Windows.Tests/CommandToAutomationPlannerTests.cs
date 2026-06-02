using Vayu.Automation.Windows;
using Vayu.Core;

namespace Vayu.Automation.Windows.Tests;

public class CommandToAutomationPlannerTests
{
    private readonly CommandToAutomationPlanner _planner = new();

    [Fact]
    public void OpenAndType_ProducesOpenStep_AndPlannedTypeStep()
    {
        var plans = _planner.PlanOpenAndType("notepad", "hello", Guid.NewGuid());

        Assert.Equal(2, plans.Count);
        Assert.Equal(AutomationActionType.OpenApp, plans[0].ActionType);
        Assert.False(plans[0].RequiresConfirmation);
        Assert.Equal(AutomationActionType.TypeText, plans[1].ActionType);
    }

    [Fact]
    public void OpenAndType_TypeStep_RequiresConfirmation_NeverAutoRuns()
    {
        // "open notepad and write hello" must NOT type — the type step is planned,
        // confirmation-gated, and never executed by the planner.
        var plans = _planner.PlanOpenAndType("notepad", "hello", Guid.NewGuid());

        var typeStep = plans[1];
        Assert.True(typeStep.RequiresConfirmation);
        Assert.Equal(RiskLevel.L3, typeStep.RiskLevel);
        Assert.Equal("hello", typeStep.TextPreview);
    }

    [Fact]
    public void OpenAndType_SecretText_TypeStepIsRejected()
    {
        var plans = _planner.PlanOpenAndType("notepad", "my password is hunter2", Guid.NewGuid());

        Assert.Equal(RiskLevel.L6, plans[1].RiskLevel); // rejected, never typed
    }

    [Fact]
    public void OpenOnly_WhenNoText_ProducesSingleOpenStep()
    {
        var plans = _planner.PlanOpenAndType("notepad", null, Guid.NewGuid());

        Assert.Single(plans);
        Assert.Equal(AutomationActionType.OpenApp, plans[0].ActionType);
    }

    [Fact]
    public void SharesCorrelationId_AcrossSteps()
    {
        var corr = Guid.NewGuid();

        var plans = _planner.PlanOpenAndType("notepad", "hello", corr);

        Assert.All(plans, p => Assert.Equal(corr, p.CorrelationId));
    }
}
