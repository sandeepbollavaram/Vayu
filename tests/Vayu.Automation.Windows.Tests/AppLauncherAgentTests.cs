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
    public async Task ExecuteAsync_Spotify_ResolvesViaKnownCatalog_NotShortcutScan()
    {
        // Regression: "open spotify" used to fall through to the Start-Menu scan
        // and fail (Spotify is a Store/MSIX app with no classic .lnk). It is now
        // a URI catalog entry, so it resolves on the static path with no shortcut
        // catalog configured at all.
        var launcher = new FakeLauncher(
            answer: CommandResult.Success(message: "Launched Spotify.", agentName: "AppLauncher"));
        var agent = new AppLauncherAgent(launcher);

        var result = await agent.ExecuteAsync(Plan(("app", "spotify")));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal("spotify", launcher.LastAppId);
        Assert.Equal(1, launcher.LaunchAsyncCallCount);
        Assert.Equal(0, launcher.LaunchShortcutCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AppPathsResolves_LaunchesExecutablePath_BeforeShortcutScan()
    {
        // An app not in the static catalog but resolvable via App Paths should
        // launch the resolved exe and never reach the shortcut scan.
        var launcher = new FakeLauncher();
        var resolver = new FakeAppPathResolver(("slack", @"C:\Apps\Slack.exe"));
        var desktop = Path.Combine(_tempRoot, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "Slack.lnk"), "fake"); // would also match
        var installed = new InstalledAppCatalog(
            new List<(string, string)> { (desktop, "UserDesktop") });
        var agent = new AppLauncherAgent(launcher, installed, resolver);

        var result = await agent.ExecuteAsync(Plan(("app", "slack")));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal(1, launcher.LaunchExecutablePathCallCount);
        Assert.Equal(@"C:\Apps\Slack.exe", launcher.LastExecutablePath);
        Assert.Equal(0, launcher.LaunchShortcutCallCount); // App Paths won, scan skipped
    }

    [Fact]
    public async Task ExecuteAsync_AppPathsMiss_FallsThroughToShortcutScan()
    {
        var launcher = new FakeLauncher();
        var resolver = new FakeAppPathResolver(); // resolves nothing
        var desktop = Path.Combine(_tempRoot, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "Slack.lnk"), "fake");
        var installed = new InstalledAppCatalog(
            new List<(string, string)> { (desktop, "UserDesktop") });
        var agent = new AppLauncherAgent(launcher, installed, resolver);

        var result = await agent.ExecuteAsync(Plan(("app", "slack")));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal(0, launcher.LaunchExecutablePathCallCount);
        Assert.Equal(1, launcher.LaunchShortcutCallCount);
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
        // Uses an app NOT in the static KnownAppCatalog (Slack) so the request
        // actually reaches the shortcut scan. (Spotify is now a static URI entry
        // and short-circuits before the scan — see the Spotify regression test.)
        var desktop = Path.Combine(_tempRoot, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "Slack.lnk"), "fake");
        var catalog = new InstalledAppCatalog(
            new List<(string, string)> { (desktop, "UserDesktop") });
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher, catalog);

        var result = await agent.ExecuteAsync(Plan(("app", "slack")));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal(0, launcher.LaunchAsyncCallCount);
        Assert.Equal(1, launcher.LaunchShortcutCallCount);
        Assert.NotNull(launcher.LastShortcut);
        Assert.Equal("Slack", launcher.LastShortcut!.DisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownApp_MultipleDistinctMatches_ReturnsNeedsClarification()
    {
        var desktop = Path.Combine(_tempRoot, "Desktop");
        Directory.CreateDirectory(desktop);
        File.WriteAllText(Path.Combine(desktop, "Slack Free.lnk"), "fake");
        File.WriteAllText(Path.Combine(desktop, "Slack Studio.lnk"), "fake");
        var catalog = new InstalledAppCatalog(
            new List<(string, string)> { (desktop, "UserDesktop") });
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher, catalog);

        var result = await agent.ExecuteAsync(Plan(("app", "slack")));

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.NotNull(result.ClarificationPrompt);
        Assert.Contains("Slack Free", result.ClarificationPrompt);
        Assert.Contains("Slack Studio", result.ClarificationPrompt);
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
        // Uses Slack (not in the static catalog) so the scan path is exercised.
        File.WriteAllText(Path.Combine(desktop, "Slack.lnk"), "fake");
        File.WriteAllText(Path.Combine(startMenu, "Slack.lnk"), "fake");
        var catalog = new InstalledAppCatalog(
            new List<(string, string)>
            {
                (desktop, "UserDesktop"),
                (startMenu, "UserStartMenu"),
            });
        var launcher = new FakeLauncher();
        var agent = new AppLauncherAgent(launcher, catalog);

        var result = await agent.ExecuteAsync(Plan(("app", "slack")));

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
        public int LaunchExecutablePathCallCount { get; private set; }
        public string? LastAppId { get; private set; }
        public InstalledAppEntry? LastShortcut { get; private set; }
        public string? LastExecutablePath { get; private set; }

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

        public Task<CommandResult> LaunchExecutablePathAsync(string executablePath, string displayName, CancellationToken cancellationToken = default)
        {
            LaunchExecutablePathCallCount++;
            LastExecutablePath = executablePath;
            return Task.FromResult(_answer);
        }
    }

    private sealed class FakeAppPathResolver : IAppPathResolver
    {
        private readonly Dictionary<string, string> _map;

        public FakeAppPathResolver(params (string AppId, string Path)[] entries)
        {
            _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (appId, path) in entries)
            {
                _map[appId] = path;
            }
        }

        public string? TryResolveExecutable(string appId)
            => _map.TryGetValue(appId, out var path) ? path : null;
    }
}
