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

    private const int MaxClarificationCandidates = 5;

    private readonly IAppLauncher _launcher;
    private readonly InstalledAppCatalog? _installed;

    /// <summary>
    /// Constructs the agent. <paramref name="installed"/> is optional —
    /// without it, only entries in <see cref="KnownAppCatalog"/> are
    /// reachable. Tests typically pass <see langword="null"/> and exercise
    /// just the static catalog path.
    /// </summary>
    public AppLauncherAgent(IAppLauncher launcher, InstalledAppCatalog? installed = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
        _installed = installed;
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

        // 1. Static catalog (Chrome, Edge, VS Code, Notepad, Terminal, Downloads + aliases).
        if (KnownAppCatalog.TryGet(app) is not null)
        {
            return await _launcher.LaunchAsync(app, cancellationToken).ConfigureAwait(false);
        }

        // 2. Installed-app discovery (Desktop + Start Menu).
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
