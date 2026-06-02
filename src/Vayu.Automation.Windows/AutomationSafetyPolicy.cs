using System.Text.RegularExpressions;

using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// The gatekeeper for desktop automation. Classifies a requested action into a
/// <see cref="RiskLevel"/>, decides whether the user must confirm, and rejects
/// anything unsafe outright — secret/credential text, shell-command text, and
/// actions against a hidden or unknown target. Pure and deterministic; holds no
/// state and touches nothing.
/// </summary>
/// <remarks>
/// This is the M5 safety contract: Vayu never types secrets, never runs shell
/// text, never acts on a window it cannot see, and never executes a higher-risk
/// action without explicit confirmation. The policy only evaluates plans — it
/// performs no automation itself.
/// </remarks>
public sealed partial class AutomationSafetyPolicy
{
    /// <summary>
    /// Returns the risk level for an action of <paramref name="actionType"/>
    /// against <paramref name="target"/> with optional <paramref name="text"/>.
    /// Unknown/secret/shell content escalates to the highest level.
    /// </summary>
    public RiskLevel Classify(AutomationActionType actionType, AutomationTarget target, string? text = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        // Secret-looking or shell-looking text is never merely "type" — it is
        // off-limits, so it classifies at the disabled level.
        if (actionType == AutomationActionType.TypeText && (LooksSecret(text) || LooksLikeShell(text)))
        {
            return RiskLevel.L6;
        }

        return actionType switch
        {
            AutomationActionType.OpenApp => RiskLevel.L1,
            AutomationActionType.FocusWindow => RiskLevel.L2,
            AutomationActionType.ReadVisibleText => RiskLevel.L2,
            AutomationActionType.Wait => RiskLevel.L0,
            AutomationActionType.TypeText => RiskLevel.L3,
            AutomationActionType.ClickElement => RiskLevel.L3,
            AutomationActionType.ScreenshotVisibleWindow => RiskLevel.L3,
            _ => RiskLevel.L6, // Unknown
        };
    }

    /// <summary>
    /// True when an action of this type requires explicit user confirmation
    /// before it may run. Everything at <see cref="RiskLevel.L3"/> or above does.
    /// </summary>
    public bool RequiresConfirmation(AutomationActionType actionType, AutomationTarget target, string? text = null)
        => Classify(actionType, target, text) >= RiskLevel.L3;

    /// <summary>
    /// Evaluates a requested action and returns a safe <see cref="AutomationActionPlan"/>,
    /// or a rejected one. The returned plan is never auto-executed — execution
    /// still requires confirmation (for L3+) and flows through the audit log.
    /// </summary>
    public AutomationActionPlan Evaluate(
        AutomationActionType actionType,
        AutomationTarget target,
        string? text,
        Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(target);

        // 1. Unknown action — never executable.
        if (actionType == AutomationActionType.Unknown)
        {
            return Reject(actionType, target, correlationId, "Vayu does not recognise this automation action.");
        }

        // 2. Acting on a window requires a resolved, visible target. (OpenApp and
        //    Wait do not need an existing window.)
        if (NeedsTarget(actionType) && !target.IsResolved)
        {
            return Reject(actionType, target, correlationId,
                "No visible target window was identified. Vayu will not act on a hidden or unknown window.");
        }

        // 3. Typing must never carry secrets or shell commands.
        if (actionType == AutomationActionType.TypeText)
        {
            if (LooksSecret(text))
            {
                return Reject(actionType, target, correlationId,
                    "That text looks like a password, key, or token. Vayu will not type secrets.");
            }
            if (LooksLikeShell(text))
            {
                return Reject(actionType, target, correlationId,
                    "That text looks like a shell command. Vayu will not type commands for execution.");
            }
        }

        var risk = Classify(actionType, target, text);
        var requiresConfirmation = risk >= RiskLevel.L3;
        var reason = Describe(actionType, target);

        return new AutomationActionPlan(
            actionType,
            target,
            risk,
            requiresConfirmation,
            reason,
            correlationId,
            TextPreview: actionType == AutomationActionType.TypeText ? Preview(text) : null);
    }

    /// <summary>
    /// Returns a rejection result when <paramref name="plan"/> must not run, or
    /// <see langword="null"/> when it is safe to proceed (subject to confirmation).
    /// </summary>
    public AutomationActionResult? RejectIfUnsafe(AutomationActionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.ActionType == AutomationActionType.Unknown)
        {
            return AutomationActionResult.Rejected(plan.ActionType, plan.CorrelationId,
                "Unrecognised automation action.");
        }
        if (NeedsTarget(plan.ActionType) && !plan.Target.IsResolved)
        {
            return AutomationActionResult.Rejected(plan.ActionType, plan.CorrelationId,
                "No visible target window.");
        }
        if (plan.RiskLevel >= RiskLevel.L6)
        {
            return AutomationActionResult.Rejected(plan.ActionType, plan.CorrelationId,
                "This action is disabled for safety.");
        }
        return null;
    }

    private static bool NeedsTarget(AutomationActionType actionType) => actionType switch
    {
        AutomationActionType.FocusWindow => true,
        AutomationActionType.ReadVisibleText => true,
        AutomationActionType.TypeText => true,
        AutomationActionType.ClickElement => true,
        AutomationActionType.ScreenshotVisibleWindow => true,
        _ => false,
    };

    private static AutomationActionPlan Reject(
        AutomationActionType actionType, AutomationTarget target, Guid correlationId, string reason)
        => new(actionType, target, RiskLevel.L6, RequiresConfirmation: false, reason, correlationId);

    private static string Describe(AutomationActionType actionType, AutomationTarget target)
    {
        var where = target.WindowTitle ?? target.AppName ?? "the target window";
        return actionType switch
        {
            AutomationActionType.OpenApp => $"Open {target.AppName ?? "the app"}.",
            AutomationActionType.FocusWindow => $"Bring {where} to the foreground.",
            AutomationActionType.ReadVisibleText => $"Read the visible text in {where}.",
            AutomationActionType.TypeText => $"Type into {where} (you approve the exact text first).",
            AutomationActionType.ClickElement => $"Click {target.ElementDescription ?? "an element"} in {where}.",
            AutomationActionType.ScreenshotVisibleWindow => $"Capture a screenshot of {where}.",
            AutomationActionType.Wait => "Wait briefly.",
            _ => "Unknown action.",
        };
    }

    /// <summary>A short, safe preview of text to be typed (capped, no secrets — those are rejected earlier).</summary>
    private static string? Preview(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        const int max = 120;
        return text.Length <= max ? text : text[..max] + "…";
    }

    /// <summary>True when the text names or looks like a credential.</summary>
    public static bool LooksSecret(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        return SecretKeywordRegex().IsMatch(text) || LongTokenRegex().IsMatch(text);
    }

    /// <summary>True when the text contains shell metacharacters or command-runner names.</summary>
    public static bool LooksLikeShell(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        return ShellRegex().IsMatch(text);
    }

    [GeneratedRegex(@"(\bapi[\s_-]?key\b|\bsecret\b|\bpassword\b|\bpasswd\b|\btoken\b|\bbearer\s+\S|\bcredential\b|\bprivate\s+key\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretKeywordRegex();

    [GeneratedRegex(@"(AIza[0-9A-Za-z_\-]{20,}|sk-[A-Za-z0-9]{16,}|ghp_[A-Za-z0-9]{20,}|[A-Za-z0-9_\-]{40,})",
        RegexOptions.CultureInvariant)]
    private static partial Regex LongTokenRegex();

    [GeneratedRegex(@"(\bcmd(\.exe)?\b|\bpowershell\b|\bpwsh\b|\bbash\b|/c\s|\-Command\b|[|&;`$]|>>|2>&1)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ShellRegex();
}
