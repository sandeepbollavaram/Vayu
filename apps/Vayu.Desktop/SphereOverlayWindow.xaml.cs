using System.Runtime.InteropServices;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

using Vayu.Voice;
using Vayu_Desktop.Controls;

using Windows.Foundation;
using Windows.Graphics;

namespace Vayu_Desktop;

/// <summary>
/// A free-floating, transparent, always-on-top desktop sphere. It is a control
/// surface — drag to move, click to open/focus the Vayu Command Center,
/// right-click for a small menu — and it visualises the voice state. It performs
/// <b>no</b> automation itself and never captures audio on its own; listening is
/// only ever started by an explicit user action.
/// </summary>
public sealed partial class SphereOverlayWindow : Window
{
    private readonly AppWindow _appWindow;
    private bool _dragging;
    private Point _dragStart;

    /// <summary>Raised when the user asks (via click or menu) to open the Command Center.</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Raised when the user asks (via menu) to start a listening session.</summary>
    public event EventHandler? StartListeningRequested;

    /// <summary>Raised when the user asks (via menu) to stop listening.</summary>
    public event EventHandler? StopListeningRequested;

    public SphereOverlayWindow()
    {
        InitializeComponent();

        _appWindow = AppWindow;
        ConfigureOverlay();
    }

    private void ConfigureOverlay()
    {
        // Borderless, always-on-top, not in the taskbar/alt-tab.
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
        _appWindow.IsShownInSwitchers = false;
        _appWindow.Resize(new SizeInt32(160, 160));

        // Make the window background truly transparent so only the sphere shows.
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // Bottom-right of the primary display by default.
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        if (area is not null)
        {
            var x = area.WorkArea.Width - 200;
            var y = area.WorkArea.Height - 220;
            _appWindow.Move(new PointInt32(x, y));
        }
    }

    /// <summary>Drives the sphere's visual to match the current voice state.</summary>
    public void SetVoiceState(VoiceInteractionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            OverlaySphere.SetVoiceState(state);
            var listening = state is VoiceInteractionState.Listening
                or VoiceInteractionState.Transcribing;
            ListeningDot.Visibility = listening ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    /// <summary>Shows or hides the listening indicator (e.g. when wake word is armed).</summary>
    public void SetListeningIndicator(bool on)
        => DispatcherQueue.TryEnqueue(() =>
            ListeningDot.Visibility = on ? Visibility.Visible : Visibility.Collapsed);

    // ---- drag to move ----

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        _dragStart = e.GetCurrentPoint(RootGrid).Position;
        RootGrid.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        var pos = e.GetCurrentPoint(RootGrid).Position;
        var dx = (int)(pos.X - _dragStart.X);
        var dy = (int)(pos.Y - _dragStart.Y);
        if (dx != 0 || dy != 0)
        {
            var p = _appWindow.Position;
            _appWindow.Move(new PointInt32(p.X + dx, p.Y + dy));
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        RootGrid.ReleasePointerCapture(e.Pointer);
    }

    // ---- click → open ----

    private void OnTapped(object sender, TappedRoutedEventArgs e)
        => OpenRequested?.Invoke(this, EventArgs.Empty);

    // ---- right-click menu ----

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var menu = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = "Open Vayu" };
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var start = new MenuFlyoutItem { Text = "Start listening" };
        start.Click += (_, _) => StartListeningRequested?.Invoke(this, EventArgs.Empty);

        var stop = new MenuFlyoutItem { Text = "Stop listening" };
        stop.Click += (_, _) => StopListeningRequested?.Invoke(this, EventArgs.Empty);

        var hide = new MenuFlyoutItem { Text = "Hide sphere" };
        hide.Click += (_, _) => _appWindow.Hide();

        menu.Items.Add(open);
        menu.Items.Add(start);
        menu.Items.Add(stop);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(hide);

        menu.ShowAt(RootGrid, e.GetPosition(RootGrid));
    }

    /// <summary>Shows the overlay if it was hidden.</summary>
    public void ShowOverlay() => _appWindow.Show();

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
