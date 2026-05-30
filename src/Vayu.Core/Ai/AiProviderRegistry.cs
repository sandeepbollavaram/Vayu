using System.Collections.Immutable;
using System.Linq;

namespace Vayu.Core.Ai;

/// <summary>
/// Canonical catalog of AI providers Vayu knows about at the contract level.
/// A registry entry does NOT imply a working HTTP connector exists — see
/// <c>docs/AI_PROVIDER_REGISTRY.md</c> for current implementation status.
/// </summary>
/// <remarks>
/// The registry is intentionally a flat <see cref="IReadOnlyList{T}"/>;
/// order is the recommended display order in the wizard's picker, grouped
/// by <see cref="AiProviderDescriptor.Kind"/>.
/// </remarks>
public static class AiProviderRegistry
{
    /// <summary>All known providers in display order.</summary>
    public static IReadOnlyList<AiProviderDescriptor> All { get; } = BuildAll();

    /// <summary>Looks up a descriptor by its stable <see cref="AiProviderDescriptor.Id"/>. Case-sensitive.</summary>
    public static AiProviderDescriptor? TryGetById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return All.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>Returns every descriptor in the given <paramref name="kind"/>.</summary>
    public static IEnumerable<AiProviderDescriptor> ByKind(AiProviderKind kind)
        => All.Where(p => p.Kind == kind);

    private static IReadOnlyList<AiProviderDescriptor> BuildAll()
    {
        return ImmutableArray.Create(
            // 1. OfflineLocal
            new AiProviderDescriptor("ollama",     "Ollama",         AiProviderKind.OfflineLocal, RequiresApiKey: false,
                SetupHelpUrl: "https://ollama.com",
                Notes: "Default. Recommended by the First Run Wizard."),
            new AiProviderDescriptor("llama-cpp",  "llama.cpp",      AiProviderKind.OfflineLocal, RequiresApiKey: false,
                SetupHelpUrl: "https://github.com/ggerganov/llama.cpp",
                Notes: "Single-binary embedded runtime. Vayu does not bundle weights."),
            new AiProviderDescriptor("lm-studio",  "LM Studio",      AiProviderKind.OfflineLocal, RequiresApiKey: false,
                SetupHelpUrl: "https://lmstudio.ai",
                Notes: "Local server with OpenAI-compatible API."),
            new AiProviderDescriptor("localai",    "LocalAI",        AiProviderKind.OfflineLocal, RequiresApiKey: false,
                SetupHelpUrl: "https://localai.io",
                Notes: "Drop-in OpenAI-compatible local server."),

            // 2. OnlineDirect
            new AiProviderDescriptor("gemini",      "Google Gemini",          AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://aistudio.google.com/app/apikey"),
            new AiProviderDescriptor("openai",      "OpenAI",                 AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://platform.openai.com/api-keys"),
            new AiProviderDescriptor("anthropic",   "Anthropic Claude",       AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://console.anthropic.com/settings/keys"),
            new AiProviderDescriptor("deepseek",    "DeepSeek",               AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://platform.deepseek.com"),
            new AiProviderDescriptor("kimi",        "Kimi (Moonshot AI)",     AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://platform.moonshot.ai"),
            new AiProviderDescriptor("mistral",     "Mistral AI",             AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://console.mistral.ai"),
            new AiProviderDescriptor("groq",        "Groq",                   AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://console.groq.com/keys"),
            new AiProviderDescriptor("cohere",      "Cohere",                 AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://dashboard.cohere.com/api-keys"),
            new AiProviderDescriptor("perplexity",  "Perplexity",             AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://www.perplexity.ai/settings/api"),
            new AiProviderDescriptor("xai",         "xAI Grok",               AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://console.x.ai"),
            new AiProviderDescriptor("together",    "Together AI",            AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://api.together.xyz/settings/api-keys"),
            new AiProviderDescriptor("fireworks",   "Fireworks AI",           AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://fireworks.ai/account/api-keys"),
            new AiProviderDescriptor("cerebras",    "Cerebras",               AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://cloud.cerebras.ai"),
            new AiProviderDescriptor("huggingface", "Hugging Face Inference", AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://huggingface.co/settings/tokens"),
            new AiProviderDescriptor("replicate",   "Replicate",              AiProviderKind.OnlineDirect, RequiresApiKey: true,
                SetupHelpUrl: "https://replicate.com/account/api-tokens"),

            // 3. CloudPlatform
            new AiProviderDescriptor("azure-openai",      "Azure OpenAI",      AiProviderKind.CloudPlatform, RequiresApiKey: true,
                SetupHelpUrl: "https://learn.microsoft.com/azure/ai-services/openai/quickstart",
                Notes: "Needs endpoint + deployment + key. Multi-field credentials."),
            new AiProviderDescriptor("aws-bedrock",       "AWS Bedrock",       AiProviderKind.CloudPlatform, RequiresApiKey: true,
                SetupHelpUrl: "https://aws.amazon.com/bedrock",
                Notes: "Authenticates via AWS credentials, not a single API key."),
            new AiProviderDescriptor("google-vertex-ai",  "Google Vertex AI",  AiProviderKind.CloudPlatform, RequiresApiKey: true,
                SetupHelpUrl: "https://cloud.google.com/vertex-ai",
                Notes: "Authenticates via GCP service account; not an AI Studio key."),

            // 4. RouterAggregator
            new AiProviderDescriptor("openrouter",                "OpenRouter",                  AiProviderKind.RouterAggregator, RequiresApiKey: true,
                SetupHelpUrl: "https://openrouter.ai/keys"),
            new AiProviderDescriptor("litellm",                   "LiteLLM-compatible endpoint", AiProviderKind.RouterAggregator, RequiresApiKey: true,
                SetupHelpUrl: "https://docs.litellm.ai",
                Notes: "User supplies base URL + key."),
            new AiProviderDescriptor("custom-openai-compatible",  "Custom OpenAI-compatible",    AiProviderKind.RouterAggregator, RequiresApiKey: true,
                SetupHelpUrl: null,
                Notes: "Power-user fallback. User supplies base URL + key.")
        );
    }
}
