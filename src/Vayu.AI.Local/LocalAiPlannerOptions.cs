namespace Vayu.AI.Local;

/// <summary>
/// Tuning knobs for <see cref="OllamaIntentPlanner"/> and the AI Router.
/// Offline planning is <b>off by default</b> — the user must opt in.
/// </summary>
public sealed record LocalAiPlannerOptions
{
    /// <summary>
    /// Master switch. When <see langword="false"/> (the default), the AI Router
    /// uses only the rule-based parser and never calls Ollama for planning.
    /// </summary>
    public bool EnableOfflinePlanning { get; init; }

    /// <summary>Model tag used for planning. Defaults to the curated recommended model.</summary>
    public string ModelTag { get; init; } = LocalModelCatalog.Recommended.ModelTag;

    /// <summary>
    /// Confidence floor (0..1). A model plan below this is discarded and the
    /// router falls back to the rule-based parser. Default 0.70.
    /// </summary>
    public double MinimumConfidence { get; init; } = 0.70;

    /// <summary>Hard cap on the prompt length sent to the model — defence against runaway input. Default 4000.</summary>
    public int MaxPromptChars { get; init; } = 4000;

    /// <summary>Per-plan timeout in seconds. Default 20.</summary>
    public int TimeoutSeconds { get; init; } = 20;
}
