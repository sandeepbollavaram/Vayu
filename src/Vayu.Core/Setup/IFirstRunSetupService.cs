namespace Vayu.Core.Setup;

/// <summary>
/// Drives the First Run Setup Wizard state machine. The interface is
/// intentionally narrow at M1 — real Ollama / Gemini work is delegated
/// to <c>IOllamaRuntimeService</c> and <c>IGeminiKeySetupService</c>,
/// which only have implementations from M2 / M3 onward.
/// </summary>
public interface IFirstRunSetupService
{
    /// <summary>
    /// Returns the persisted setup snapshot, or <see cref="FirstRunSetupState.Empty"/>
    /// on a fresh install. Always safe to call; never performs I/O against
    /// Ollama or Gemini.
    /// </summary>
    Task<FirstRunSetupState> GetStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Begins (or restarts) the wizard with the chosen mode and returns the
    /// new working state. Does NOT install Ollama, pull a model, or save a
    /// Gemini key — those happen as the user advances through wizard pages.
    /// </summary>
    Task<FirstRunSetupState> StartAsync(FirstRunSetupMode mode, CancellationToken ct = default);

    /// <summary>
    /// Marks the wizard as complete and persists the final state. Safe to call
    /// even if some steps were skipped — Vayu must remain usable in that case.
    /// </summary>
    Task<FirstRunSetupState> CompleteAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears the persisted state so the wizard reopens on the next launch.
    /// Does not delete any installed Ollama models or stored Gemini keys.
    /// </summary>
    Task ResetAsync(CancellationToken ct = default);
}
