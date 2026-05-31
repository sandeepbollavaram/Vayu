using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.Voice;

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
/// Bounded 320×320 2D AI-core visual matching the Vayu icon identity:
/// glowing teal/cyan orb with a layered wind "V" (two-arm strokes meeting
/// at the bottom) and a tight dotted particle ring rotating around the orb.
/// </summary>
/// <remarks>
/// All visuals are declared in <c>VayuSphere.xaml</c>. The dotted ring is
/// a single Ellipse with <c>StrokeDashArray + StrokeDashCap=Round</c> so the
/// rotation pivots around the orb centre and the dots cannot drift across
/// the page. No code-generated children.
///
/// TODO (M11): swap this 2D scene for a true 3D / particle voice-reactive
/// visualisation driven by microphone amplitude and STT state.
/// </remarks>
public sealed partial class VayuSphere : UserControl
{
    private static readonly Color SuccessColor = Color.FromArgb(0xFF, 0x2E, 0xE6, 0xC9);
    private static readonly Color ErrorColor   = Color.FromArgb(0xFF, 0xFF, 0x5C, 0x7C);

    public VayuSphere()
    {
        InitializeComponent();
        Loaded += (_, _) => IdleStoryboard.Begin();
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
                // The idle storyboard already runs forever; nothing to do.
                break;
        }
    }

    /// <summary>
    /// Maps a voice interaction state onto the sphere's existing visual states
    /// (M4.2 foundation). Full voice-reactive animation is M4.6.
    /// </summary>
    public void SetVoiceState(VoiceInteractionState voiceState)
    {
        switch (voiceState)
        {
            case VoiceInteractionState.Listening:
            case VoiceInteractionState.Transcribing:
            case VoiceInteractionState.Thinking:
            case VoiceInteractionState.Executing:
                SetState(VayuSphereState.Processing);
                break;
            case VoiceInteractionState.Error:
                SetState(VayuSphereState.Error);
                break;
            case VoiceInteractionState.Speaking:
                SetState(VayuSphereState.Success);
                break;
            case VoiceInteractionState.Cancelled:
            case VoiceInteractionState.Idle:
            default:
                SetState(VayuSphereState.Idle);
                break;
        }
    }

    private void Flash(Color color)
    {
        StatusFlashStop.Color = color;
        FlashStoryboard.Begin();
    }
}
