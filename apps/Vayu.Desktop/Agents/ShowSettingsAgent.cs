using Vayu.Core;
using Vayu_Desktop.Services;

namespace Vayu_Desktop.Agents;

/// <summary>
/// Handles the <c>ui.show_settings</c> intent by asking the shell to
/// navigate to the Settings page.
/// </summary>
public sealed class ShowSettingsAgent : IAgent
{
    public const string AgentName = "ShowSettings";
    public const string Intent = "ui.show_settings";

    private readonly UiNavigationService _navigation;

    public ShowSettingsAgent(UiNavigationService navigation)
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
        _navigation.RequestNavigate("settings");
        return Task.FromResult(CommandResult.Success(
            message: "Opened the Settings page.",
            agentName: AgentName,
            plan: plan));
    }
}
