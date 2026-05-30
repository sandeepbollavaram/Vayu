using System.Collections.Immutable;

using Vayu.Automation.Windows;
using Vayu.Core;

namespace Vayu.Automation.Windows.Tests;

public class AppLauncherAgentTests
{
    [Fact]
    public void Declares_Identity()
    {
        var agent = new AppLauncherAgent(new FakeLauncher());

        Assert.Equal("AppLauncher", agent.Name);
        Assert.Equal(RiskLevel.L1, agent.MaxRisk);
        Assert.Contains("app.open", agent.Intents);
        Assert.Single(agent.Intents);
    }

    [Fact]
    public async Task ExecuteAsync_WithKnownApp_CallsLauncher()
    {
        var launcher = new FakeLauncher(
            answer: CommandResult.Success(message: "Launched.", agentName: "AppLauncher"));
        var agent = new AppLauncherAgent(launcher);
        var plan = Plan(("app", "notepad"));

        var result = await agent.ExecuteAsync(plan);

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal("notepad", launcher.LastAppId);
        Assert.Equal(1, launcher.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsLauncherFailure()
    {
        var launcher = new FakeLauncher(
            answer: CommandResult.Failed("Unknown app.", errorCode: "UNKNOWN_APP", agentName: "AppLauncher"));
        var agent = new AppLauncherAgent(launcher);
        var plan = Plan(("app", "spaceship"));

        var result = await agent.ExecuteAsync(plan);

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("UNKNOWN_APP", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_MissingAppArg_ReturnsNeedsClarification_WithoutLaunching()
    {
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher);
        var plan = Plan(); // no args

        var result = await agent.ExecuteAsync(plan);

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ClarificationPrompt));
        Assert.Equal(0, launcher.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_BlankAppArg_ReturnsNeedsClarification(string blank)
    {
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher);
        var plan = Plan(("app", blank));

        var result = await agent.ExecuteAsync(plan);

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.Equal(0, launcher.CallCount);
    }

    [Fact]
    public void Constructor_Throws_OnNullLauncher()
    {
        Assert.Throws<ArgumentNullException>(() => new AppLauncherAgent(null!));
    }

    [Fact]
    public async Task ExecuteAsync_Throws_OnNullPlan()
    {
        var agent = new AppLauncherAgent(new FakeLauncher());

        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.ExecuteAsync(null!));
    }

    private static IntentPlan Plan(params (string Key, string Value)[] args)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in args)
        {
            builder[key] = value;
        }
        return new IntentPlan
        {
            Intent = "app.open",
            Risk = RiskLevel.L1,
            Args = builder.ToImmutable(),
        };
    }

    private sealed class FakeLauncher : IAppLauncher
    {
        private readonly CommandResult _answer;

        public FakeLauncher(CommandResult? answer = null)
        {
            _answer = answer ?? CommandResult.Success(agentName: "AppLauncher");
        }

        public int CallCount { get; private set; }
        public string? LastAppId { get; private set; }

        public Task<CommandResult> LaunchAsync(string appId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastAppId = appId;
            return Task.FromResult(_answer);
        }
    }
}
