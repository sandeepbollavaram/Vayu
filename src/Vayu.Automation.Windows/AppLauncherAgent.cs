using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// The <see cref="IAgent"/> that handles <c>app.open</c> intents. Pulls
/// the target app from <see cref="IntentPlan.Args"/> (key <c>"app"</c>)
/// and asks <see cref="IAppLauncher"/> to launch it.
/// </summary>
/// <remarks>
/// Declares <see cref="MaxRisk"/> = <see cref="RiskLevel.L1"/>: this
/// agent can open known apps and folders, nothing more. Plans with
/// higher risk are refused by the runtime before they reach this agent.
/// </remarks>
public sealed class AppLauncherAgent : IAgent
{
    /// <summary>The agent name registered in <c>AgentRegistry</c>.</summary>
    public const string AgentName = "AppLauncher";

    /// <summary>The single intent this agent handles.</summary>
    public const string AppOpenIntent = "app.open";

    /// <summary>The argument key the parser/planner uses to identify the target app.</summary>
    public const string AppArgKey = "app";

    private readonly IAppLauncher _launcher;

    public AppLauncherAgent(IAppLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        _launcher = launcher;
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

        return await _launcher.LaunchAsync(app, cancellationToken).ConfigureAwait(false);
    }
}
