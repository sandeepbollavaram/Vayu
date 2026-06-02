using Vayu.Automation.Windows;
using Vayu.Core;

namespace Vayu.Automation.Windows.Tests;

public class AutomationSafetyPolicyTests
{
    private readonly AutomationSafetyPolicy _policy = new();
    private static AutomationTarget VisibleNotepad => new(AppName: "Notepad", WindowTitle: "Untitled - Notepad");

    [Theory]
    [InlineData(AutomationActionType.TypeText)]
    [InlineData(AutomationActionType.ClickElement)]
    [InlineData(AutomationActionType.ScreenshotVisibleWindow)]
    public void RiskyActions_RequireConfirmation(AutomationActionType action)
    {
        Assert.True(_policy.RequiresConfirmation(action, VisibleNotepad, "hello"));
    }

    [Theory]
    [InlineData(AutomationActionType.OpenApp)]
    [InlineData(AutomationActionType.FocusWindow)]
    [InlineData(AutomationActionType.ReadVisibleText)]
    [InlineData(AutomationActionType.Wait)]
    public void LowRiskActions_DoNotRequireConfirmation(AutomationActionType action)
    {
        Assert.False(_policy.RequiresConfirmation(action, VisibleNotepad));
    }

    [Theory]
    [InlineData("my password is hunter2")]
    [InlineData("API key: anything")]
    [InlineData("bearer abc.def")]
    [InlineData("here is my secret token value")]
    public void TypeText_SecretLikeText_IsRejected(string secretText)
    {
        var plan = _policy.Evaluate(AutomationActionType.TypeText, VisibleNotepad, secretText, Guid.NewGuid());

        Assert.NotNull(_policy.RejectIfUnsafe(plan));
        Assert.Equal(RiskLevel.L6, plan.RiskLevel);
    }

    [Fact]
    public void TypeText_LongTokenLikeText_IsRejected()
    {
        // A 40+ char token-shaped string, built at runtime so no key-shaped
        // literal is committed (the repo secret scanner would flag one).
        var tokenLike = new string('Z', 44);

        var plan = _policy.Evaluate(AutomationActionType.TypeText, VisibleNotepad, tokenLike, Guid.NewGuid());

        Assert.NotNull(_policy.RejectIfUnsafe(plan));
    }

    [Theory]
    [InlineData("powershell -Command Get-Process")]
    [InlineData("cmd.exe /c del *")]
    [InlineData("echo hi && rm -rf x")]
    [InlineData("type x | findstr y")]
    public void TypeText_ShellLikeText_IsRejected(string shellText)
    {
        var plan = _policy.Evaluate(AutomationActionType.TypeText, VisibleNotepad, shellText, Guid.NewGuid());

        Assert.NotNull(_policy.RejectIfUnsafe(plan));
    }

    [Fact]
    public void TypeText_PlainText_IsAllowed_ButRequiresConfirmation()
    {
        var plan = _policy.Evaluate(AutomationActionType.TypeText, VisibleNotepad, "hello world", Guid.NewGuid());

        Assert.Null(_policy.RejectIfUnsafe(plan));   // safe to proceed
        Assert.True(plan.RequiresConfirmation);       // but only after approval
        Assert.Equal(RiskLevel.L3, plan.RiskLevel);
        Assert.Equal("hello world", plan.TextPreview);
    }

    [Fact]
    public void WindowAction_WithUnknownTarget_IsRejected()
    {
        var plan = _policy.Evaluate(AutomationActionType.TypeText, AutomationTarget.Unknown, "hello", Guid.NewGuid());

        Assert.NotNull(_policy.RejectIfUnsafe(plan));
    }

    [Fact]
    public void UnknownAction_IsRejected()
    {
        var plan = _policy.Evaluate(AutomationActionType.Unknown, VisibleNotepad, null, Guid.NewGuid());

        Assert.NotNull(_policy.RejectIfUnsafe(plan));
    }

    [Fact]
    public void Screenshot_OfVisibleWindow_RequiresConfirmation_NotRejected()
    {
        var plan = _policy.Evaluate(AutomationActionType.ScreenshotVisibleWindow, VisibleNotepad, null, Guid.NewGuid());

        Assert.Null(_policy.RejectIfUnsafe(plan));
        Assert.True(plan.RequiresConfirmation);
    }

    [Fact]
    public void ReadVisibleText_OfVisibleWindow_IsAllowed_WithoutConfirmation()
    {
        var plan = _policy.Evaluate(AutomationActionType.ReadVisibleText, VisibleNotepad, null, Guid.NewGuid());

        Assert.Null(_policy.RejectIfUnsafe(plan));
        Assert.False(plan.RequiresConfirmation);
        Assert.Equal(RiskLevel.L2, plan.RiskLevel);
    }
}
