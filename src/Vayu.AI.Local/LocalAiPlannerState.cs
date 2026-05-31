namespace Vayu.AI.Local;

/// <summary>
/// Process-level, user-flippable state for offline AI planning. The
/// Settings toggle writes <see cref="OfflinePlanningEnabled"/>; the AI
/// Router reads it on every plan so the user can turn local AI on or off
/// without restarting Vayu.
/// </summary>
/// <remarks>
/// Defaults to <see langword="false"/> — offline planning is opt-in. The
/// seed value comes from <see cref="LocalAiPlannerOptions.EnableOfflinePlanning"/>
/// so configuration and runtime state agree at startup.
/// </remarks>
public sealed class LocalAiPlannerState
{
    private volatile bool _enabled;

    public LocalAiPlannerState(bool initiallyEnabled = false)
    {
        _enabled = initiallyEnabled;
    }

    /// <summary>True when the AI Router should attempt local planning before falling back.</summary>
    public bool OfflinePlanningEnabled
    {
        get => _enabled;
        set => _enabled = value;
    }
}
