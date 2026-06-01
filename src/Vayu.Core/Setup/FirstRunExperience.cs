namespace Vayu.Core.Setup;

/// <summary>
/// What the app shell should show at launch, decided purely from the persisted
/// <see cref="FirstRunSetupState"/>.
/// </summary>
public enum FirstRunRouting
{
    /// <summary>
    /// First launch (or a reset). Show the dedicated first-run setup experience
    /// before the main Vayu Command Center.
    /// </summary>
    ShowFirstRunSetup = 0,

    /// <summary>
    /// Setup has already been completed or explicitly skipped at least once.
    /// Go straight to the main Command Center.
    /// </summary>
    EnterCommandCenter = 1,
}

/// <summary>
/// Decides, from persisted setup state alone, whether Vayu should open its
/// dedicated first-run setup experience or go straight to the Command Center.
/// </summary>
/// <remarks>
/// This is the seam that lets the in-app "Setup" page grow into a real
/// first-launch experience: <c>MainWindow</c> calls <see cref="DecideRouting"/>
/// once at startup and either routes into setup or into the main shell. The
/// helper is intentionally pure and UI-free so it can be unit-tested and reused
/// by a future installer-style launch flow.
/// <para>
/// M4.R deliberately does NOT make first-run setup a hard, blocking gate yet —
/// Vayu must stay usable even if a user backs out — so the desktop shell may
/// treat <see cref="FirstRunRouting.ShowFirstRunSetup"/> as a strong default
/// (navigate to setup first) rather than a lock. The "completed OR skipped"
/// semantics live in <see cref="FirstRunSetupState.Completed"/>: completing or
/// skipping the wizard both set it, so the experience is shown at most once
/// until <see cref="IFirstRunSetupService.ResetAsync"/> is called.
/// </para>
/// </remarks>
public static class FirstRunExperience
{
    /// <summary>
    /// Returns <see cref="FirstRunRouting.ShowFirstRunSetup"/> only when the
    /// wizard has never been completed or skipped; otherwise routes straight to
    /// the Command Center.
    /// </summary>
    public static FirstRunRouting DecideRouting(FirstRunSetupState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Completed
            ? FirstRunRouting.EnterCommandCenter
            : FirstRunRouting.ShowFirstRunSetup;
    }

    /// <summary>
    /// Convenience over <see cref="DecideRouting"/>: true when the shell should
    /// open first-run setup before the main app.
    /// </summary>
    public static bool ShouldShowFirstRunSetup(FirstRunSetupState state)
        => DecideRouting(state) == FirstRunRouting.ShowFirstRunSetup;
}
