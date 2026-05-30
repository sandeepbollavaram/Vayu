using Vayu.Core;
using Vayu_Desktop.Services;

namespace Vayu_Desktop.Agents;

/// <summary>
/// Handles the <c>ui.show_logs</c> intent by asking the shell to navigate
/// to the Logs page. Pure UI side-effect, no I/O, no risk above L0.
/// </summary>
public sealed class ShowLogsAgent : IAgent
{
    public const string AgentName = "ShowLogs";
    public const string Intent = "ui.show_logs";

    private readonly UiNavigationService _navigation;

    public ShowLogsAgent(UiNavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        _navigation = navigation;
    }

    public string Name => AgentName;
    public RiskLevel MaxRisk => RiskLevel.L0;
    public IReadOnlyCollection<string> Intents { get; } = new[] { Intent };

    public Task<CommandResult> ExecuteAsync(IntentPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _navigation.RequestNavigate("logs");
        return Task.FromResult(CommandResult.Success(
            message: "Opened the Logs page.",
            agentName: AgentName,
            plan: plan));
    }
}
