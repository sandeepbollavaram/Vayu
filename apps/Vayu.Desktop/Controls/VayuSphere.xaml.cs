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

    // M4.6 voice-state accent colours.
    private static readonly Color ListenCyan  = Color.FromArgb(0xFF, 0x00, 0xE1, 0xFF);
    private static readonly Color ScanBlue    = Color.FromArgb(0xFF, 0x4F, 0xA8, 0xFF);
    private static readonly Color ThinkViolet = Color.FromArgb(0xFF, 0x9B, 0x7B, 0xFF);
    private static readonly Color ExecuteTeal = Color.FromArgb(0xFF, 0x2E, 0xE6, 0xC9);
    private static readonly Color SpeakTeal   = Color.FromArgb(0xFF, 0x2E, 0xE6, 0xC9);
    private static readonly Color CancelAmber = Color.FromArgb(0xFF, 0xFF, 0xB8, 0x5C);

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
    /// Drives the sphere for a voice interaction state (M4.6). Each state gets a
    /// distinct, lightweight cue via <see cref="VoiceStateVisualMapper"/>; the
    /// app-command visuals (<see cref="SetState"/>) are untouched.
    /// </summary>
    public void SetVoiceState(VoiceInteractionState voiceState)
    {
        // Stop any continuous voice storyboard from a previous state so repeated
        // push-to-talk/stop never leaves a stuck animation.
        StopVoiceStoryboards();

        var token = VoiceStateVisualMapper.Map(voiceState);
        switch (token)
        {
            case VoiceVisualToken.ListeningPulse:
                SetRingColor(ListenCyan);
                ListeningStoryboard.Begin();
                break;
            case VoiceVisualToken.TranscribingScan:
                SetRingColor(ScanBlue);
                ProcessingStoryboard.Begin();
                break;
            case VoiceVisualToken.ThinkingAccent:
                SetRingColor(ThinkViolet);
                ProcessingStoryboard.Begin();
                break;
            case VoiceVisualToken.ExecutingRing:
                SetRingColor(ExecuteTeal);
                ProcessingStoryboard.Begin();
                break;
            case VoiceVisualToken.SpeakingGlow:
                StatusFlashStop.Color = SpeakTeal;
                SpeakingStoryboard.Begin();
                break;
            case VoiceVisualToken.ErrorFlash:
                Flash(ErrorColor);
                break;
            case VoiceVisualToken.CancelledFade:
                Flash(CancelAmber);
                break;
            case VoiceVisualToken.IdlePulse:
            default:
                // The idle storyboard already runs forever; just clear overlays.
                ResetVoiceOverlays();
                break;
        }
    }

    private void StopVoiceStoryboards()
    {
        ListeningStoryboard.Stop();
        SpeakingStoryboard.Stop();
        ResetVoiceOverlays();
    }

    private void ResetVoiceOverlays()
    {
        // Return the shared overlay elements to their calm baseline.
        ProcessingRing.Opacity = 0;
        PulseHalo.Opacity = 0.6;
        StatusFlash.Opacity = 0;
    }

    private void SetRingColor(Color color)
        => ProcessingRing.Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);

    private void Flash(Color color)
    {
        StatusFlashStop.Color = color;
        FlashStoryboard.Begin();
    }
}
