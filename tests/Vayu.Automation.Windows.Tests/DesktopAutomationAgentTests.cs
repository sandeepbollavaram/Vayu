using System.Collections.Immutable;

using Vayu.Automation.Windows;
using Vayu.Core;

namespace Vayu.Automation.Windows.Tests;

public class DesktopAutomationAgentTests
{
    [Fact]
    public void Declares_L3_Identity()
    {
        var agent = Build(out _, out _, out _);

        Assert.Equal("DesktopAutomation", agent.Name);
        Assert.Equal(RiskLevel.L3, agent.MaxRisk);
        Assert.Contains("desktop.open_and_type", agent.Intents);
    }

    [Fact]
    public async Task ApproveOnce_LaunchesAndTypesOnce()
    {
        var agent = Build(out var launcher, out var confirm, out var typing,
            decision: AutomationConfirmationDecision.ApproveOnce);

        var result = await agent.ExecuteAsync(Plan("notepad", "hello"));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal(1, launcher.LaunchCount);
        Assert.Equal(1, confirm.RequestCount);
        Assert.Equal(1, typing.TypeCount);
        Assert.Equal("hello", typing.LastText);
    }

    [Fact]
    public async Task Cancel_DoesNotType()
    {
        var agent = Build(out _, out var confirm, out var typing,
            decision: AutomationConfirmationDecision.Cancel);

        var result = await agent.ExecuteAsync(Plan("notepad", "hello"));

        Assert.Equal(CommandStatus.Cancelled, result.Status);
        Assert.Equal(1, confirm.RequestCount);
        Assert.Equal(0, typing.TypeCount); // nothing typed on cancel
    }

    [Theory]
    [InlineData("my password is hunter2")]
    [InlineData("powershell -Command whoami")]
    public async Task SecretOrShellText_BlockedBeforeLaunchOrConfirm(string unsafeText)
    {
        var agent = Build(out var launcher, out var confirm, out var typing,
            decision: AutomationConfirmationDecision.ApproveOnce);

        var result = await agent.ExecuteAsync(Plan("notepad", unsafeText));

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("AUTOMATION_REJECTED", result.ErrorCode);
        // Rejected up front: never opened the app, never prompted, never typed.
        Assert.Equal(0, launcher.LaunchCount);
        Assert.Equal(0, confirm.RequestCount);
        Assert.Equal(0, typing.TypeCount);
    }

    [Fact]
    public async Task NoVisibleWindow_FailsSafe_WithoutTyping()
    {
        var agent = Build(out _, out var confirm, out var typing,
            decision: AutomationConfirmationDecision.ApproveOnce,
            windows: new FakeWindows()); // discovers nothing

        var result = await agent.ExecuteAsync(Plan("notepad", "hello"));

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("AUTOMATION_NO_TARGET", result.ErrorCode);
        Assert.Equal(0, confirm.RequestCount);
        Assert.Equal(0, typing.TypeCount);
    }

    [Fact]
    public async Task MissingApp_ReturnsNeedsClarification()
    {
        var agent = Build(out _, out _, out _);

        var result = await agent.ExecuteAsync(Plan(app: null, "hello"));

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
    }

    [Fact]
    public async Task TypingFailure_MapsToFailedResult()
    {
        var agent = Build(out _, out _, out var typing,
            decision: AutomationConfirmationDecision.ApproveOnce);
        typing.NextResultFails = true;

        var result = await agent.ExecuteAsync(Plan("notepad", "hello"));

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("AUTOMATION_TYPE_FAILED", result.ErrorCode);
    }

    // ---- helpers / fakes ----

    private static IntentPlan Plan(string? app, string text)
    {
        var args = ImmutableDictionary<string, string>.Empty;
        if (app is not null)
        {
            args = args.Add("app", app);
        }
        args = args.Add("text", text);
        return new IntentPlan { Intent = "desktop.open_and_type", Risk = RiskLevel.L3, Args = args };
    }

    private static DesktopAutomationAgent Build(
        out FakeLauncher launcher,
        out FakeConfirm confirm,
        out FakeTyping typing,
        AutomationConfirmationDecision decision = AutomationConfirmationDecision.Cancel,
        FakeWindows? windows = null)
    {
        launcher = new FakeLauncher();
        confirm = new FakeConfirm(decision);
        typing = new FakeTyping();
        windows ??= new FakeWindows(new WindowInfo("Untitled - Notepad", "notepad", 1234, true));
        return new DesktopAutomationAgent(
            launcher, windows, new AutomationSafetyPolicy(), confirm, typing,
            delay: (_, _) => Task.CompletedTask);
    }

    private sealed class FakeLauncher : IAppLauncher
    {
        public int LaunchCount { get; private set; }
        public Task<CommandResult> LaunchAsync(string appId, CancellationToken ct = default)
        {
            LaunchCount++;
            return Task.FromResult(CommandResult.Success("ok", "AppLauncher"));
        }
        public Task<CommandResult> LaunchShortcutAsync(InstalledAppEntry entry, CancellationToken ct = default)
            => Task.FromResult(CommandResult.Success());
        public Task<CommandResult> LaunchExecutablePathAsync(string path, string name, CancellationToken ct = default)
            => Task.FromResult(CommandResult.Success());
    }

    private sealed class FakeWindows : IWindowDiscoveryService
    {
        private readonly IReadOnlyList<WindowInfo> _windows;
        public FakeWindows(params WindowInfo[] windows) => _windows = windows;
        public Task<IReadOnlyList<WindowInfo>> GetVisibleWindowsAsync(CancellationToken ct = default)
            => Task.FromResult(_windows);
    }

    private sealed class FakeConfirm : IAutomationConfirmationService
    {
        private readonly AutomationConfirmationDecision _decision;
        public FakeConfirm(AutomationConfirmationDecision decision) => _decision = decision;
        public int RequestCount { get; private set; }
        public Task<AutomationConfirmationDecision> RequestAsync(AutomationConfirmationRequest request, CancellationToken ct = default)
        {
            RequestCount++;
            return Task.FromResult(_decision);
        }
    }

    private sealed class FakeTyping : ITextTypingExecutor
    {
        public int TypeCount { get; private set; }
        public string? LastText { get; private set; }
        public bool NextResultFails { get; set; }
        public Task<AutomationActionResult> TypeTextAsync(AutomationTarget target, string text, Guid correlationId, CancellationToken ct = default)
        {
            TypeCount++;
            LastText = text;
            return Task.FromResult(NextResultFails
                ? new AutomationActionResult(false, AutomationStatus.Failed, "no target", AutomationActionType.TypeText, correlationId)
                : new AutomationActionResult(true, AutomationStatus.Success, "typed", AutomationActionType.TypeText, correlationId));
        }
    }
}
