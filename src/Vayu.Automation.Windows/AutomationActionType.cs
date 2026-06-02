namespace Vayu.Automation.Windows;

/// <summary>
/// The kinds of desktop automation Vayu can <em>plan</em>. Planning a higher-risk
/// action does not execute it — execution is gated by
/// <see cref="AutomationSafetyPolicy"/> + user confirmation + the audit log.
/// </summary>
public enum AutomationActionType
{
    /// <summary>Unrecognised / not yet classified. Always treated as unsafe to execute.</summary>
    Unknown = 0,

    /// <summary>Launch a known app (the existing M1 capability).</summary>
    OpenApp = 1,

    /// <summary>Bring a visible top-level window to the foreground.</summary>
    FocusWindow = 2,

    /// <summary>Read visible text/metadata from a window. No clicks or typing.</summary>
    ReadVisibleText = 3,

    /// <summary>Type text into an approved, visible app. Confirmation required.</summary>
    TypeText = 4,

    /// <summary>Click a described UI element in an approved, visible app. Confirmation required.</summary>
    ClickElement = 5,

    /// <summary>Capture a screenshot of a visible window. Explicit permission required.</summary>
    ScreenshotVisibleWindow = 6,

    /// <summary>Pause for a bounded interval between steps.</summary>
    Wait = 7,
}
