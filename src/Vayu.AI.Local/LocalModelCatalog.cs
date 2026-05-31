using System.Collections.Immutable;

namespace Vayu.AI.Local;

/// <summary>
/// Curated list of local models Vayu's First Run Wizard offers. Three
/// entries cover the common hardware tiers: a modern-laptop default
/// (Gemma 3 4B), a low-end fallback (Gemma 3 1B), and an alternative
/// for users who already run Llama (Llama 3.2 3B).
/// </summary>
/// <remarks>
/// Adding a new model is a deliberate code change so the wizard's
/// "recommended" badge stays curated. The catalog is purely a data
/// contract — it never triggers an install, never downloads weights,
/// and never talks to the network. Discovery of *installed* models
/// arrives with <c>IOllamaRuntimeService.ListLocalModelsAsync</c> in M2.4.
/// </remarks>
public static class LocalModelCatalog
{
    /// <summary>All curated models in display order. The recommended entry is first.</summary>
    public static IReadOnlyList<LocalModelDescriptor> All { get; } = BuildAll();

    /// <summary>The one entry flagged <see cref="LocalModelDescriptor.IsRecommended"/> = true.</summary>
    public static LocalModelDescriptor Recommended { get; } = ResolveRecommended(All);

    private static readonly ImmutableDictionary<string, LocalModelDescriptor> ByTag =
        All.ToImmutableDictionary(m => m.ModelTag, m => m, StringComparer.OrdinalIgnoreCase);

    /// <summary>Case-insensitive tag lookup. Returns <see langword="null"/> when unknown.</summary>
    public static LocalModelDescriptor? FindByTag(string modelTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelTag);
        return ByTag.TryGetValue(modelTag.Trim(), out var descriptor) ? descriptor : null;
    }

    /// <summary>Alias for <see cref="All"/> for callers that prefer the verb.</summary>
    public static IReadOnlyList<LocalModelDescriptor> ListAll() => All;

    private static IReadOnlyList<LocalModelDescriptor> BuildAll() =>
    [
        new LocalModelDescriptor(
            ModelTag: "gemma3:4b",
            DisplayName: "Gemma 3 · 4B",
            Provider: "Google Gemma",
            Description: "Vayu's recommended default for command parsing and general planning.",
            RecommendedUse: "default",
            HardwareNote: "~3 GB RAM, comfortable on a modern laptop CPU/iGPU.",
            IsRecommended: true),

        new LocalModelDescriptor(
            ModelTag: "gemma3:1b",
            DisplayName: "Gemma 3 · 1B",
            Provider: "Google Gemma",
            Description: "Smallest Gemma; still useful for short commands when memory is tight.",
            RecommendedUse: "low-end hardware",
            HardwareNote: "~1 GB RAM, works on integrated GPUs and 8 GB systems.",
            IsRecommended: false),

        new LocalModelDescriptor(
            ModelTag: "llama3.2:3b",
            DisplayName: "Llama 3.2 · 3B",
            Provider: "Meta Llama",
            Description: "Solid 3 B alternative for users who already use Llama.",
            RecommendedUse: "alternative",
            HardwareNote: "~2 GB RAM.",
            IsRecommended: false),
    ];

    private static LocalModelDescriptor ResolveRecommended(IReadOnlyList<LocalModelDescriptor> all)
    {
        foreach (var model in all)
        {
            if (model.IsRecommended)
            {
                return model;
            }
        }
        throw new InvalidOperationException("LocalModelCatalog has no entry flagged as recommended.");
    }
}
