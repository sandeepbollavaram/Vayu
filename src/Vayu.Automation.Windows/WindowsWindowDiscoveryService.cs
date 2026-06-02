using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Vayu.Automation.Windows;

/// <summary>
/// Real <see cref="IWindowDiscoveryService"/> over the Win32 windowing API.
/// Enumerates only <b>visible top-level windows with a title</b> via
/// <c>EnumWindows</c> + <c>IsWindowVisible</c> + <c>GetWindowText</c>, resolves
/// the owning process name, and reads the on-screen rectangle. It never reads
/// window contents, never enumerates hidden/owned windows, and never captures
/// a screenshot.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsWindowDiscoveryService : IWindowDiscoveryService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<WindowInfo>> GetVisibleWindowsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var windows = new List<WindowInfo>();
        try
        {
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true; // skip hidden windows
                }

                var length = GetWindowTextLength(hWnd);
                if (length <= 0)
                {
                    return true; // skip untitled windows (tool windows, shells, etc.)
                }

                var sb = new StringBuilder(length + 1);
                _ = GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title))
                {
                    return true;
                }

                GetWindowThreadProcessId(hWnd, out uint pid);
                var processName = ResolveProcessName((int)pid);

                WindowBounds? bounds = null;
                if (GetWindowRect(hWnd, out var rect))
                {
                    bounds = new WindowBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
                }

                windows.Add(new WindowInfo(title, processName, (int)pid, IsVisible: true, bounds));
                return true;
            }, IntPtr.Zero);
        }
#pragma warning disable CA1031 // Discovery must never throw into the caller; return what we have.
        catch (Exception)
        {
            // Return whatever was collected before the failure.
        }
#pragma warning restore CA1031

        return Task.FromResult<IReadOnlyList<WindowInfo>>(windows);
    }

    private static string ResolveProcessName(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            return p.ProcessName;
        }
#pragma warning disable CA1031 // A vanished/inaccessible process must not break enumeration.
        catch (Exception)
        {
            return "unknown";
        }
#pragma warning restore CA1031
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
