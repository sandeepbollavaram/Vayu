using System.Collections.Immutable;

using Vayu.Automation.Windows;
using Vayu.Core;

namespace Vayu.Automation.Windows.Tests;

public class AppLauncherAgentTests : IDisposable
{
    private readonly string _tempRoot;

    public AppLauncherAgentTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"vayu-agt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

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
    public async Task ExecuteAsync_TypingRequested_ReturnsNeedsClarification_WithoutLaunching()
    {
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher);

        var plan = Plan(
            ("app", "notepad"),
            (AppLauncherAgent.TypingRequestedArgKey, "true"));

        var result = await agent.ExecuteAsync(plan);

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.NotNull(result.ClarificationPrompt);
        Assert.Contains("M5", result.ClarificationPrompt);
        Assert.Contains("notepad", result.ClarificationPrompt);
        // Critically: even with a known app, no launch happens when typing was requested.
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
        Assert.Equal(0, launcher.LaunchShortcutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_KnownApp_CallsLauncherLaunchAsync()
    {
        var launcher = new FakeLauncher(
            answer: CommandResult.Success(message: "Launched.", agentName: "AppLauncher"));
        var agent = new AppLauncherAgent(launcher);

        var result = await agent.ExecuteAsync(Plan(("app", "notepad")));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal("notepad", launcher.LastAppId);
        Assert.Equal(1, launcher.LaunchAsyncCallCount);
        Assert.Equal(0, launcher.LaunchShortcutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownApp_NoInstalledCatalog_ReturnsFailedUnknown()
    {
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher);

        var result = await agent.ExecuteAsync(Plan(("app", "spaceship")));

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("UNKNOWN_APP", result.ErrorCode);
        // The launcher was never called — the agent rejected before reaching it.
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
        Assert.Equal(0, launcher.LaunchShortcutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownApp_InstalledCatalogSingleMatch_CallsLaunchShortcut()
    {
        var desktop = Path.Combine(_tempRoot, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "Spotify.lnk"), "fake");
        var catalog = new InstalledAppCatalog(
            new List<(string, string)> { (desktop, "UserDesktop") });
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher, catalog);

        var result = await agent.ExecuteAsync(Plan(("app", "spotify")));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
        Assert.Equal(1, launcher.LaunchShortcutCallCount);
        Assert.NotNull(launcher.LastShortcut);
        Assert.Equal("Spotify", launcher.LastShortcut!.DisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownApp_MultipleDistinctMatches_ReturnsNeedsClarification()
    {
        var desktop = Path.Combine(_tempRoot, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "Spotify Free.lnk"), "fake");
        File.WriteAllText(Path.Combine(desktop, "Spotify Studio.lnk"), "fake");
        var catalog = new InstalledAppCatalog(
            new List<(string, string)> { (desktop, "UserDesktop") });
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher, catalog);

        var result = await agent.ExecuteAsync(Plan(("app", "spotify")));

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.NotNull(result.ClarificationPrompt);
        Assert.Contains("Spotify Free", result.ClarificationPrompt);
        Assert.Contains("Spotify Studio", result.ClarificationPrompt);
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
        Assert.Equal(0, launcher.LaunchShortcutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownApp_DedupesByNormalisedDisplayName()
    {
        var desktop = Path.Combine(_tempRoot, "Desktop");
        var startMenu = Path.Combine(_tempRoot, "StartMenu");
        Directory.CreateDirectory(desktop);
        Directory.CreateDirectory(startMenu);
        // Same app, two roots — dedup by display name should collapse to one launch.
        File.WriteAllText(Path.Combine(desktop, "Spotify.lnk"), "fake");
        File.WriteAllText(Path.Combine(startMenu, "Spotify.lnk"), "fake");
        var catalog = new InstalledAppCatalog(
            new List<(string, string)>
            {
                (desktop, "UserDesktop"),
                (startMenu, "UserStartMenu"),
            });
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher, catalog);

        var result = await agent.ExecuteAsync(Plan(("app", "spotify")));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal(1, launcher.LaunchShortcutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownApp_InstalledCatalogNoMatch_ReturnsFailedUnknown()
    {
        var desktop = Path.Combine(_tempRoot, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "Slack.lnk"), "fake");
        var catalog = new InstalledAppCatalog(
            new List<(string, string)> { (desktop, "UserDesktop") });
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher, catalog);

        var result = await agent.ExecuteAsync(Plan(("app", "spaceship")));

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("UNKNOWN_APP", result.ErrorCode);
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
        Assert.Equal(0, launcher.LaunchShortcutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MissingAppArg_ReturnsNeedsClarification_WithoutLaunching()
    {
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher);

        var result = await agent.ExecuteAsync(Plan()); // no args

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ClarificationPrompt));
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_BlankAppArg_ReturnsNeedsClarification(string blank)
    {
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher);

        var result = await agent.ExecuteAsync(Plan(("app", blank)));

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
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

        public int LaunchAsyncCallCount { get; private set; }
        public int LaunchShortcutCallCount { get; private set; }
        public string? LastAppId { get; private set; }
        public InstalledAppEntry? LastShortcut { get; private set; }

        public Task<CommandResult> LaunchAsync(string appId, CancellationToken cancellationToken = default)
        {
            LaunchAsyncCallCount++;
            LastAppId = appId;
            return Task.FromResult(_answer);
        }

        public Task<CommandResult> LaunchShortcutAsync(InstalledAppEntry entry, CancellationToken cancellationToken = default)
        {
            LaunchShortcutCallCount++;
            LastShortcut = entry;
            return Task.FromResult(_answer);
        }
    }
}
