using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// Handles the <c>desktop.open_and_type</c> intent — the first real advanced
/// desktop automation: open an app, then (only after explicit user approval)
/// type approved text into its visible window.
/// </summary>
/// <remarks>
/// Safety layering (none bypassed):
/// <list type="number">
/// <item>The runtime's permission engine has already gated this L3 plan.</item>
/// <item>The app is launched through the existing safe <see cref="IAppLauncher"/> path.</item>
/// <item>The target window must be found <b>visible</b> via <see cref="IWindowDiscoveryService"/>.</item>
/// <item><see cref="AutomationSafetyPolicy"/> rejects secret/shell text and unknown targets <em>before</em> any prompt.</item>
/// <item>The user must <b>Approve once</b> in <see cref="IAutomationConfirmationService"/> — Cancel types nothing.</item>
/// <item>Typing goes through <see cref="ITextTypingExecutor"/> (UI Automation, never global SendKeys).</item>
/// </list>
/// The agent never logs the raw text; the runtime writes the (redacted) audit row.
/// </remarks>
public sealed class DesktopAutomationAgent : IAgent
{
    /// <summary>The agent name registered in the runtime.</summary>
    public const string AgentName = "DesktopAutomation";

    /// <summary>The intent this agent handles.</summary>
    public const string OpenAndTypeIntent = "desktop.open_and_type";

    /// <summary>Args key for the app to open.</summary>
    public const string AppArgKey = "app";

    /// <summary>Args key for the text to type.</summary>
    public const string TextArgKey = "text";

    private const int WindowPollAttempts = 20;
    private const int WindowPollDelayMs = 150;

    private readonly IAppLauncher _launcher;
    private readonly IWindowDiscoveryService _windows;
    private readonly AutomationSafetyPolicy _policy;
    private readonly IAutomationConfirmationService _confirmation;
    private readonly ITextTypingExecutor _typing;
    private readonly Func<int, CancellationToken, Task> _delay;

    public DesktopAutomationAgent(
        IAppLauncher launcher,
        IWindowDiscoveryService windows,
        AutomationSafetyPolicy policy,
        IAutomationConfirmationService confirmation,
        ITextTypingExecutor typing,
        Func<int, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(typing);
        _launcher = launcher;
        _windows = windows;
        _policy = policy;
        _confirmation = confirmation;
        _typing = typing;
        _delay = delay ?? ((ms, ct) => Task.Delay(ms, ct));
    }

    /// <inheritdoc />
    public string Name => AgentName;

    /// <inheritdoc />
    public RiskLevel MaxRisk => RiskLevel.L3;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Intents { get; } = new[] { OpenAndTypeIntent };

    /// <inheritdoc />
    public async Task<CommandResult> ExecuteAsync(IntentPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.Args.TryGetValue(AppArgKey, out var app) || string.IsNullOrWhiteSpace(app))
        {
            return CommandResult.NeedsClarification("Which app should I open and type into?", plan);
        }
        plan.Args.TryGetValue(TextArgKey, out var text);

        // 1. Evaluate the typing plan BEFORE doing anything else, so unsafe text
        //    (secrets / shell) is blocked before we even open the app or prompt.
        var typePlan = _policy.Evaluate(
            AutomationActionType.TypeText,
            new AutomationTarget(AppName: app, WindowTitle: app),
            text,
            plan.CorrelationId);
        if (_policy.RejectIfUnsafe(typePlan) is { } rejection)
        {
            return CommandResult.Failed(
                $"Desktop automation blocked: {rejection.Message}",
                errorCode: "AUTOMATION_REJECTED",
                agentName: AgentName,
                plan: plan);
        }

        // 2. Open the app through the existing safe launch path.
        var launch = await _launcher.LaunchAsync(app, cancellationToken).ConfigureAwait(false);
        if (launch.Status != CommandStatus.Success)
        {
            return launch;
        }

        // 3. Find the now-visible target window (bounded poll).
        var window = await WaitForVisibleWindowAsync(app, cancellationToken).ConfigureAwait(false);
        if (window is null)
        {
            return CommandResult.Failed(
                $"Opened {app}, but could not find its visible window to type into.",
                errorCode: "AUTOMATION_NO_TARGET",
                agentName: AgentName,
                plan: plan);
        }

        var resolvedTarget = new AutomationTarget(
            AppName: window.ProcessName,
            WindowTitle: window.Title,
            ProcessId: window.ProcessId);

        // 4. Ask for explicit approval, showing the exact text preview.
        var request = AutomationConfirmationRequest.FromPlan(
            typePlan with { Target = resolvedTarget },
            "Vayu will type only the approved text into the selected visible window. Do not approve if the target looks wrong.");
        var decision = await _confirmation.RequestAsync(request, cancellationToken).ConfigureAwait(false);
        if (decision != AutomationConfirmationDecision.ApproveOnce)
        {
            return CommandResult.Cancelled("Desktop automation cancelled. Nothing was typed.", plan);
        }

        // 5. Type the approved text via UI Automation (no global SendKeys).
        var result = await _typing
            .TypeTextAsync(resolvedTarget, text ?? string.Empty, plan.CorrelationId, cancellationToken)
            .ConfigureAwait(false);

        return result.Success
            ? CommandResult.Success($"Typed approved text into {resolvedTarget.AppName ?? app}.", AgentName, plan)
            : CommandResult.Failed(result.Message, errorCode: "AUTOMATION_TYPE_FAILED", agentName: AgentName, plan: plan);
    }

    private async Task<WindowInfo?> WaitForVisibleWindowAsync(string app, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < WindowPollAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var windows = await _windows.GetVisibleWindowsAsync(cancellationToken).ConfigureAwait(false);
            var match = windows.FirstOrDefault(w => Matches(w, app));
            if (match is not null)
            {
                return match;
            }
            await _delay(WindowPollDelayMs, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }

    private static bool Matches(WindowInfo window, string app)
    {
        var a = Normalize(app);
        return Normalize(window.ProcessName).Contains(a, StringComparison.Ordinal)
            || Normalize(window.Title).Contains(a, StringComparison.Ordinal);
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }
        var chars = s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
        return new string(chars);
    }
}
