namespace Vayu.Automation.Windows;

/// <summary>
/// The app/window an automation action targets. Vayu only ever acts on a
/// <b>visible, identified</b> target — a hidden or unknown target is rejected by
/// <see cref="AutomationSafetyPolicy"/>.
/// </summary>
/// <param name="AppName">Friendly app name (e.g. <c>Notepad</c>); null when unknown.</param>
/// <param name="WindowTitle">The visible window title; null when unknown.</param>
/// <param name="ProcessId">The target process id, when known.</param>
/// <param name="ElementDescription">A human description of the UI element (for click/type); null otherwise.</param>
public sealed record AutomationTarget(
    string? AppName = null,
    string? WindowTitle = null,
    int? ProcessId = null,
    string? ElementDescription = null)
{
    /// <summary>
    /// True when the target identifies a concrete app or window — the minimum
    /// bar before any action may be planned against it.
    /// </summary>
    public bool IsResolved =>
        !string.IsNullOrWhiteSpace(AppName) || !string.IsNullOrWhiteSpace(WindowTitle);

    /// <summary>An explicitly empty/unknown target.</summary>
    public static AutomationTarget Unknown { get; } = new();
}
