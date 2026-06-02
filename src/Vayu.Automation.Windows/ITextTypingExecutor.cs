namespace Vayu.Automation.Windows;

/// <summary>
/// Types approved text into an approved, visible window. The concrete Windows
/// implementation uses UI Automation patterns (e.g. <c>ValuePattern</c>) — never
/// global blind <c>SendKeys</c> — and fails safe when no editable control can be
/// found. Implementations must already have an approved
/// <see cref="AutomationActionPlan"/> (confirmed by the user) before being called.
/// </summary>
public interface ITextTypingExecutor
{
    /// <summary>
    /// Types <paramref name="text"/> into the window identified by
    /// <paramref name="target"/>. Returns a success/failure result; never throws
    /// into the caller, never types secrets (the caller rejects those first), and
    /// never types into a window other than the resolved target.
    /// </summary>
    Task<AutomationActionResult> TypeTextAsync(
        AutomationTarget target,
        string text,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
