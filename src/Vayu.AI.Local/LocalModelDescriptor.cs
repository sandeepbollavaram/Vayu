namespace Vayu.AI.Local;

/// <summary>
/// Static metadata about a model that Vayu's offline AI layer knows how
/// to use. Discovery and pull-with-consent flows (M2.2 / M2.4 / M2.6)
/// reference these descriptors so the UI can show recommended picks
/// without hardcoding strings in multiple places.
/// </summary>
/// <param name="ModelTag">Pullable tag accepted by the local runtime, e.g. <c>gemma3:4b</c>.</param>
/// <param name="DisplayName">Human-readable label shown in the wizard's model picker.</param>
/// <param name="Provider">Upstream provider/family of the model, e.g. <c>"Google Gemma"</c>, <c>"Meta Llama"</c>.</param>
/// <param name="Description">One-line description of what this model is good at.</param>
/// <param name="RecommendedUse">Short hint (e.g. "default", "low-end hardware", "alternative").</param>
/// <param name="HardwareNote">Rough hardware/RAM guidance, e.g. <c>"~3 GB RAM"</c>.</param>
/// <param name="IsRecommended">True for exactly one entry in the catalog — the safe default for new installs.</param>
public sealed record LocalModelDescriptor(
    string ModelTag,
    string DisplayName,
    string Provider,
    string Description,
    string RecommendedUse,
    string HardwareNote,
    bool IsRecommended);
