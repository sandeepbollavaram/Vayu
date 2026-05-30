namespace Vayu.Core;

/// <summary>
/// Risk classification for an action Vayu is about to take. The permission
/// engine uses this to decide whether the user must confirm before the
/// action runs.
/// </summary>
/// <remarks>
/// Levels are ordered: a higher numeric value means more potentially
/// disruptive. Anything at or above <see cref="L3"/> requires confirmation
/// by default; <see cref="L6"/> is disabled outright unless explicitly
/// unlocked in code.
/// </remarks>
public enum RiskLevel
{
    /// <summary>Read-only observation. No side effects. Example: report battery level.</summary>
    L0 = 0,

    /// <summary>Open an app or folder the user already has installed. Example: <c>open chrome</c>.</summary>
    L1 = 1,

    /// <summary>Read visible window metadata (title, focused element). No clicks or typing.</summary>
    L2 = 2,

    /// <summary>Type or click inside an approved app. Confirmation required.</summary>
    L3 = 3,

    /// <summary>Send a message, email, or any outbound communication. Confirmation required.</summary>
    L4 = 4,

    /// <summary>Run a shell or terminal command. Confirmation required.</summary>
    L5 = 5,

    /// <summary>Administrative / system-level action (install software, change registry). Disabled by default.</summary>
    L6 = 6,
}
