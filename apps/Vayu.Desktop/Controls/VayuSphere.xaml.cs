using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

using Windows.UI;

namespace Vayu_Desktop.Controls;

/// <summary>
/// Visual state of the <see cref="VayuSphere"/>. Drives small animations
/// that signal what the runtime is doing without taking real screen space.
/// </summary>
public enum VayuSphereState
{
    /// <summary>Slow breathing pulse + dot-ring rotation.</summary>
    Idle,

    /// <summary>Faster pulse + accent ring flash while a command is in flight.</summary>
    Processing,

    /// <summary>Short cyan/teal glow flash after a successful command.</summary>
    Success,

    /// <summary>Short red/amber glow flash after a failed or cancelled command.</summary>
    Error,
}

/// <summary>
/// Lightweight 2D AI-core visual that mirrors the Vayu app icon:
/// a glowing cyan/teal orb with a flowing wind "V" inside, ringed by a
/// dotted particle halo. Pure XAML + storyboards.
/// </summary>
/// <remarks>
/// TODO (M4+): swap this 2D scene for a voice-reactive particle / 3D
/// visualization driven by microphone amplitude and STT state.
/// </remarks>
public sealed partial class VayuSphere : UserControl
{
    private const int DotCount = 36;
    private const double DotRadius = 2.4;
    private const double RingRadius = 168;
    private const double CanvasSize = 360;

    private static readonly Color SuccessColor = Color.FromArgb(0xFF, 0x2E, 0xE6, 0xC9);
    private static readonly Color ErrorColor   = Color.FromArgb(0xFF, 0xFF, 0x5C, 0x7C);
    private static readonly Color DotColor     = Color.FromArgb(0xFF, 0x9F, 0xF0, 0xE8);

    public VayuSphere()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DotRingCanvas.Children.Count == 0)
        {
            BuildDottedRing();
        }
        IdleStoryboard.Begin();
    }

    private void BuildDottedRing()
    {
        var centre = CanvasSize / 2.0;
        var brush = new SolidColorBrush(DotColor);

        for (var i = 0; i < DotCount; i++)
        {
            var angle = (i * 360.0 / DotCount) * Math.PI / 180.0;
            var x = centre + (RingRadius * Math.Cos(angle));
            var y = centre + (RingRadius * Math.Sin(angle));

            // Every 4th dot is brighter — gives the halo a varied "particle" feel.
            var bright = i % 4 == 0;
            var diameter = bright ? DotRadius * 2.2 : DotRadius * 2.0;

            var dot = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = brush,
                Opacity = bright ? 1.0 : 0.55,
            };

            Canvas.SetLeft(dot, x - (diameter / 2));
            Canvas.SetTop(dot, y - (diameter / 2));
            DotRingCanvas.Children.Add(dot);
        }
    }

    /// <summary>Switches the sphere to the given visual state.</summary>
    public void SetState(VayuSphereState state)
    {
        switch (state)
        {
            case VayuSphereState.Processing:
                ProcessingStoryboard.Begin();
                break;
            case VayuSphereState.Success:
                Flash(SuccessColor);
                break;
            case VayuSphereState.Error:
                Flash(ErrorColor);
                break;
            case VayuSphereState.Idle:
            default:
                // Idle storyboard already runs forever; nothing to do.
                break;
        }
    }

    private void Flash(Color color)
    {
        StatusFlashStop.Color = color;
        FlashStoryboard.Begin();
    }
}
