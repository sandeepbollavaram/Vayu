namespace Vayu.Voice;

/// <summary>
/// Whether a microphone is available and Vayu is permitted to use it. Used to
/// gate the push-to-talk affordance. Reports status only — it never opens the
/// device.
/// </summary>
/// <param name="IsAvailable">True when an input device is present.</param>
/// <param name="PermissionGranted">True when the OS mic permission is granted to Vayu.</param>
/// <param name="DeviceName">Optional human-friendly device name; null when unknown.</param>
/// <param name="Message">Short, redaction-safe explanation for the UI.</param>
public sealed record MicrophoneStatus(
    bool IsAvailable,
    bool PermissionGranted,
    string? DeviceName,
    string Message)
{
    /// <summary>True only when capture is allowed: a device exists and permission is granted.</summary>
    public bool CanCapture => IsAvailable && PermissionGranted;

    /// <summary>A safe "no microphone / not permitted" status.</summary>
    public static MicrophoneStatus Unavailable(string? message = null)
        => new(IsAvailable: false, PermissionGranted: false, DeviceName: null,
               Message: message ?? "No microphone is available or permission has not been granted.");

    /// <summary>A "ready to capture" status.</summary>
    public static MicrophoneStatus Ready(string? deviceName = null, string? message = null)
        => new(IsAvailable: true, PermissionGranted: true, DeviceName: deviceName,
               Message: message ?? "Microphone is ready.");
}
