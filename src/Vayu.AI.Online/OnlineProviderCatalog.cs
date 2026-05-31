using System.Collections.Immutable;

namespace Vayu.AI.Online;

/// <summary>
/// Curated catalog of online providers Vayu plans to support. Pure data: no
/// provider is enabled, no key is held, no call is made. Adding a provider is
/// a deliberate code change.
/// </summary>
/// <remarks>
/// <see cref="RecommendedFirstProvider"/> is <c>gemini</c> only because it is
/// the first concrete connector Vayu implements (M3.2) — <b>not</b> because it
/// is the only or preferred provider. Vayu is multi-provider by design; see
/// <c>docs/AI_PROVIDER_REGISTRY.md</c>.
/// </remarks>
public static class OnlineProviderCatalog
{
    /// <summary>All known providers in display order.</summary>
    public static IReadOnlyList<OnlineProviderDescriptor> All { get; } = BuildAll();

    // Declared before the public RecommendedFirstProvider property so it is
    // initialized first (static field/property initializers run top to bottom).
    private static readonly ImmutableDictionary<string, OnlineProviderDescriptor> ById =
        All.ToImmutableDictionary(p => p.ProviderId, p => p, StringComparer.OrdinalIgnoreCase);

    /// <summary>The provider Vayu implements first (Gemini). First, not only.</summary>
    public static OnlineProviderDescriptor RecommendedFirstProvider { get; } = FindOrThrow("gemini");

    /// <summary>Case-insensitive id lookup. Returns <see langword="null"/> when unknown.</summary>
    public static OnlineProviderDescriptor? FindById(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return ById.TryGetValue(providerId.Trim(), out var descriptor) ? descriptor : null;
    }

    /// <summary>Alias for <see cref="All"/>.</summary>
    public static IReadOnlyList<OnlineProviderDescriptor> ListAll() => All;

    private static OnlineProviderDescriptor FindOrThrow(string id)
        => FindById(id) ?? throw new InvalidOperationException($"OnlineProviderCatalog is missing required provider '{id}'.");

    private static IReadOnlyList<OnlineProviderDescriptor> BuildAll() =>
    [
        new("gemini", "Google Gemini", OnlineProviderKind.DirectApi,
            "Google's Gemini models. The first cloud provider Vayu implements (M3.2).",
            "docs/GEMINI_SETUP.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("openai", "OpenAI", OnlineProviderKind.DirectApi,
            "OpenAI GPT models via the official API.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("claude", "Anthropic Claude", OnlineProviderKind.DirectApi,
            "Anthropic's Claude models via the Messages API.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("deepseek", "DeepSeek", OnlineProviderKind.DirectApi,
            "DeepSeek chat and reasoning models (OpenAI-compatible API).",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("kimi", "Moonshot Kimi", OnlineProviderKind.DirectApi,
            "Moonshot AI's Kimi models (OpenAI-compatible API).",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("mistral", "Mistral", OnlineProviderKind.DirectApi,
            "Mistral AI hosted models.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("groq", "Groq", OnlineProviderKind.DirectApi,
            "Groq's low-latency inference for open models.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("cohere", "Cohere", OnlineProviderKind.DirectApi,
            "Cohere Command models.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: false, SupportsStreaming: true),

        new("perplexity", "Perplexity", OnlineProviderKind.DirectApi,
            "Perplexity's online-search-augmented models.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: false, SupportsStreaming: true),

        new("xai", "xAI Grok", OnlineProviderKind.DirectApi,
            "xAI's Grok models (OpenAI-compatible API).",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("openrouter", "OpenRouter", OnlineProviderKind.Router,
            "Aggregator fronting many models behind a single key.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("litellm", "LiteLLM", OnlineProviderKind.CustomOpenAiCompatible,
            "Self-hosted LiteLLM proxy exposing an OpenAI-compatible endpoint.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),

        new("custom-openai-compatible", "Custom (OpenAI-compatible)", OnlineProviderKind.CustomOpenAiCompatible,
            "Any user-supplied endpoint that speaks the OpenAI chat-completions protocol.",
            "docs/AI_PROVIDER_REGISTRY.md", SupportsChat: true, SupportsJsonMode: true, SupportsStreaming: true),
    ];
}
