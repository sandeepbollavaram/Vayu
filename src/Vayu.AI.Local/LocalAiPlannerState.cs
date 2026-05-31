namespace Vayu.AI.Local;

/// <summary>
/// Process-level, user-flippable state for AI planning. The Settings AI Mode
/// card writes it; the AI Router reads it on every plan so the user can change
/// modes without restarting Vayu.
/// </summary>
/// <remarks>
/// Defaults to <see cref="PlanningMode.RuleBased"/> — every AI mode is opt-in.
/// <see cref="OfflinePlanningEnabled"/> is kept for back-compat and is now
/// derived from <see cref="Mode"/> (Offline or Hybrid). The seed value comes
/// from <see cref="LocalAiPlannerOptions.EnableOfflinePlanning"/> so config and
/// runtime agree at startup.
/// </remarks>
public sealed class LocalAiPlannerState
{
    private readonly object _gate = new();
    private PlanningMode _mode;
    private string? _activeModelTag;
    private string? _activeOnlineProviderId;

    public LocalAiPlannerState(bool initiallyEnabled = false)
    {
        _mode = initiallyEnabled ? PlanningMode.Offline : PlanningMode.RuleBased;
    }

    /// <summary>The active planning mode. The single source of truth for routing.</summary>
    public PlanningMode Mode
    {
        get { lock (_gate) { return _mode; } }
        set { lock (_gate) { _mode = value; } }
    }

    /// <summary>
    /// Back-compat flag — true when the mode uses the local model (Offline or
    /// Hybrid). Setting it maps to Offline (true) / RuleBased (false), preserving
    /// the M2.7/M2.8 toggle behaviour.
    /// </summary>
    public bool OfflinePlanningEnabled
    {
        get => Mode is PlanningMode.Offline or PlanningMode.Hybrid;
        set => Mode = value ? PlanningMode.Offline : PlanningMode.RuleBased;
    }

    /// <summary>True when the mode uses a cloud provider (Online or Hybrid).</summary>
    public bool OnlinePlanningEnabled => Mode is PlanningMode.Online or PlanningMode.Hybrid;

    /// <summary>
    /// The installed curated model the local planner should run, chosen by
    /// <see cref="LocalAiReadiness.SelectActiveModel"/> after the last detection
    /// refresh. <see langword="null"/> when no curated model is installed.
    /// </summary>
    public string? ActiveModelTag
    {
        get { lock (_gate) { return _activeModelTag; } }
        set { lock (_gate) { _activeModelTag = value; } }
    }

    /// <summary>
    /// The online provider id the router should use in Online/Hybrid mode, e.g.
    /// <c>"gemini"</c>. <see langword="null"/> when no online provider is selected.
    /// </summary>
    public string? ActiveOnlineProviderId
    {
        get { lock (_gate) { return _activeOnlineProviderId; } }
        set { lock (_gate) { _activeOnlineProviderId = value; } }
    }
}
