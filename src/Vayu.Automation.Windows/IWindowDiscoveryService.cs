namespace Vayu.Automation.Windows;

/// <summary>
/// Enumerates the user's <b>visible top-level</b> windows so automation can
/// target a window the user can actually see. Read-only and non-intrusive:
/// implementations must never enumerate hidden windows, never read window
/// contents, and never capture screenshots.
/// </summary>
public interface IWindowDiscoveryService
{
    /// <summary>
    /// Returns the visible top-level windows (title + owning process + bounds).
    /// Never opens, focuses, or modifies any window.
    /// </summary>
    Task<IReadOnlyList<WindowInfo>> GetVisibleWindowsAsync(CancellationToken cancellationToken = default);
}
