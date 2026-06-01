using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// The <see cref="IAgent"/> that handles <c>app.open</c> intents. Resolves
/// the target app in three steps:
/// <list type="number">
/// <item>Static <see cref="KnownAppCatalog"/> lookup (chrome, edge, vscode, notepad, terminal, downloads).</item>
/// <item>If unknown and an <see cref="InstalledAppCatalog"/> is configured, search the user's Desktop and Start Menu for a matching shortcut.</item>
/// <item>If still nothing, return <see cref="CommandStatus.Failed"/> with <c>UNKNOWN_APP</c>.</item>
/// </list>
/// When the installed-app search finds multiple distinct matches, the
/// agent returns <see cref="CommandStatus.NeedsClarification"/> with the
/// candidate names so the user can disambiguate.
/// </summary>
/// <remarks>
/// Declares <see cref="MaxRisk"/> = <see cref="RiskLevel.L1"/>: this agent
/// can open known apps, known folders, and discovered desktop shortcuts —
/// nothing more. The launcher never accepts arbitrary file paths from user
/// text; only catalog-issued entries are launched.
/// </remarks>
public sealed class AppLauncherAgent : IAgent
{
    /// <summary>The agent name registered in <c>AgentRegistry</c>.</summary>
    public const string AgentName = "AppLauncher";

    /// <summary>The single intent this agent handles.</summary>
    public const string AppOpenIntent = "app.open";

    /// <summary>The argument key the parser/planner uses to identify the target app.</summary>
    public const string AppArgKey = "app";

    /// <summary>
    /// Args key set by the parser when the user asked to type/write into
    /// the app (e.g. "open notepad and write hello"). The agent refuses
    /// such plans in M1 — typing/clicking inside apps is M5 Advanced
    /// Desktop Automation and will require explicit user confirmation.
    /// </summary>
    public const string TypingRequestedArgKey = "typing_requested";

    private const int MaxClarificationCandidates = 5;

    private readonly IAppLauncher _launcher;
    private readonly InstalledAppCatalog? _installed;
    private readonly IAppPathResolver? _appPaths;

    /// <summary>
    /// Constructs the agent. <paramref name="installed"/> and
    /// <paramref name="appPaths"/> are optional — without them, only entries in
    /// <see cref="KnownAppCatalog"/> are reachable. Tests typically pass
    /// <see langword="null"/> and exercise just the static catalog path.
    /// </summary>
    public AppLauncherAgent(
        IAppLauncher launcher,
        InstalledAppCatalog? installed = null,
        IAppPathResolver? appPaths = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
        _installed = installed;
        _appPaths = appPaths;
    }

    /// <inheritdoc />
    public string Name => AgentName;

    /// <inheritdoc />
    public RiskLevel MaxRisk => RiskLevel.L1;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Intents { get; } = new[] { AppOpenIntent };

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(IntentPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.Args.TryGetValue(AppArgKey, out var app) || string.IsNullOrWhiteSpace(app))
        {
            return CommandResult.NeedsClarification(
                "Which app would you like me to open?",
                plan);
        }

        // M1 cannot type into apps. If the parser flagged a typing
        // request, refuse the plan with a clear pointer to M5.
        // (TODO M5: Advanced Desktop Automation — explicit confirmation required.)
        if (plan.Args.TryGetValue(TypingRequestedArgKey, out var typingFlag) &&
            string.Equals(typingFlag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.NeedsClarification(
                $"M1 can open {app} but typing into apps lands in M5 Advanced Desktop Automation. Try 'open {app}'.",
                plan);
        }

        // 1. Static catalog (known apps, folders, vetted URI schemes + aliases).
        if (KnownAppCatalog.TryGet(app) is not null)
        {
            return await _launcher.LaunchAsync(app, cancellationToken).ConfigureAwait(false);
        }

        // 2. Windows App Paths registry — catches normal installs (e.g. Spotify's
        //    classic installer) that register an executable but leave no shortcut.
        //    Resolves to a validated .exe path only; never a command line.
        if (_appPaths is not null && OperatingSystem.IsWindows())
        {
            var exe = _appPaths.TryResolveExecutable(app);
            if (exe is not null)
            {
                return await _launcher
                    .LaunchExecutablePathAsync(exe, app, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // 3. Installed-app discovery (Desktop + Start Menu shortcuts).
        if (_installed is not null)
        {
            var matches = await _installed.SearchAsync(app, cancellationToken).ConfigureAwait(false);
            var distinct = DistinctByNormalisedName(matches);

            if (distinct.Count == 1)
            {
                return await _launcher.LaunchShortcutAsync(distinct[0], cancellationToken).ConfigureAwait(false);
            }
            if (distinct.Count > 1)
            {
                var top = string.Join(
                    ", ",
                    distinct
                        .Take(MaxClarificationCandidates)
                        .Select(m => m.DisplayName));
                return CommandResult.NeedsClarification(
                    $"Multiple apps match '{app}'. Try one of: {top}.",
                    plan);
            }
        }

        // 3. Nothing matched.
        return CommandResult.Failed(
            $"'{app}' is not in Vayu's app catalog and no matching desktop or Start Menu shortcut was found.",
            errorCode: "UNKNOWN_APP",
            agentName: Name,
            plan: plan);
    }

    private static IReadOnlyList<InstalledAppEntry> DistinctByNormalisedName(IReadOnlyList<InstalledAppEntry> matches)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<InstalledAppEntry>(matches.Count);
        foreach (var entry in matches)
        {
            if (seen.Add(InstalledAppCatalog.Normalize(entry.DisplayName)))
            {
                distinct.Add(entry);
            }
        }
        return distinct;
    }
}
