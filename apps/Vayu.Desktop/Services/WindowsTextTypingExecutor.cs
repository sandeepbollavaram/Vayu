using System.Runtime.Versioning;

using Interop.UIAutomationClient;

using Vayu.Automation.Windows;

namespace Vayu_Desktop.Services;

/// <summary>
/// Types approved text into an approved, visible window using <b>UI Automation</b>
/// (<c>ValuePattern.SetValue</c> via the official <c>Interop.UIAutomationClient</c>)
/// — never global blind <c>SendKeys</c>. Resolves the target window by process id,
/// finds an editable control exposing the Value pattern, and sets its value. If no
/// safe editable control is found, it fails clearly and types nothing.
/// </summary>
/// <remarks>
/// This is the only place Windows UI Automation is touched. The caller
/// (<see cref="DesktopAutomationAgent"/>) has already rejected secret/shell text
/// and obtained explicit user approval, so this executor only performs the
/// already-approved, already-safe action.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsTextTypingExecutor : ITextTypingExecutor
{
    /// <inheritdoc />
    public Task<AutomationActionResult> TypeTextAsync(
        AutomationTarget target,
        string text,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (string.IsNullOrEmpty(text))
        {
            return Task.FromResult(AutomationActionResult.Cancelled(
                AutomationActionType.TypeText, correlationId, "Nothing to type."));
        }
        if (target.ProcessId is not { } pid)
        {
            return Task.FromResult(Fail(correlationId, "No resolved target window to type into."));
        }

        try
        {
            var automation = new CUIAutomation();
            var root = automation.GetRootElement();

            // Find the top-level window owned by the approved process.
            var pidCondition = automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ProcessIdPropertyId, pid);
            var window = root.FindFirst(TreeScope.TreeScope_Children, pidCondition);
            if (window is null)
            {
                return Task.FromResult(Fail(correlationId, "Could not find the approved window."));
            }

            // Find an editable descendant that exposes the Value pattern.
            var valueCondition = automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_IsValuePatternAvailablePropertyId, true);
            var editable = window.FindFirst(TreeScope.TreeScope_Descendants, valueCondition);
            if (editable is null)
            {
                return Task.FromResult(Fail(correlationId,
                    "Could not safely find an editable target to type into."));
            }

            if (editable.GetCurrentPattern(UIA_PatternIds.UIA_ValuePatternId)
                is not IUIAutomationValuePattern valuePattern)
            {
                return Task.FromResult(Fail(correlationId,
                    "The target does not support safe text entry."));
            }
            if (valuePattern.CurrentIsReadOnly != 0)
            {
                return Task.FromResult(Fail(correlationId, "The target is read-only."));
            }

            valuePattern.SetValue(text);
            return Task.FromResult(new AutomationActionResult(
                true, AutomationStatus.Success, "Typed approved text.",
                AutomationActionType.TypeText, correlationId));
        }
#pragma warning disable CA1031 // UI Automation boundary: any COM/interop failure → safe message, types nothing.
        catch (Exception)
        {
            return Task.FromResult(Fail(correlationId,
                "Could not safely type into the target window."));
        }
#pragma warning restore CA1031
    }

    private static AutomationActionResult Fail(Guid correlationId, string message)
        => new(false, AutomationStatus.Failed, message, AutomationActionType.TypeText, correlationId);
}
