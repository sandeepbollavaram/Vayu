namespace Vayu.Automation.Windows;

/// <summary>
/// Metadata about a single <b>visible top-level</b> window. Contains only what
/// the user can already see (title, owning process, on-screen bounds) — never
/// window contents, never hidden windows, never a screenshot.
/// </summary>
/// <param name="Title">The visible window title.</param>
/// <param name="ProcessName">The owning process name (e.g. <c>notepad</c>).</param>
/// <param name="ProcessId">The owning process id.</param>
/// <param name="IsVisible">Always true for discovered windows; modelled for clarity.</param>
/// <param name="Bounds">On-screen rectangle, when known.</param>
public sealed record WindowInfo(
    string Title,
    string ProcessName,
    int ProcessId,
    bool IsVisible,
    WindowBounds? Bounds = null);

/// <summary>An on-screen rectangle in pixels.</summary>
public readonly record struct WindowBounds(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Width in pixels.</summary>
    public int Width => Right - Left;

    /// <summary>Height in pixels.</summary>
    public int Height => Bottom - Top;
}
