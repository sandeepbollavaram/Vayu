namespace Vayu.Core.Ai;

/// <summary>
/// High-level category of an AI provider in the
/// <see cref="AiProviderRegistry"/>. The First Run Setup Wizard groups
/// providers by this kind when presenting the picker.
/// </summary>
public enum AiProviderKind
{
    /// <summary>Self-hosted local runtime — no API key, no internet. e.g. Ollama, llama.cpp.</summary>
    OfflineLocal = 0,

    /// <summary>First-party API endpoint reached over the public internet with a single API key. e.g. Gemini, OpenAI.</summary>
    OnlineDirect = 1,

    /// <summary>Hyperscaler-hosted model with cloud-provider authentication (resource id, region, multi-field credentials). e.g. Azure OpenAI, AWS Bedrock.</summary>
    CloudPlatform = 2,

    /// <summary>A router or aggregator endpoint that fans out to many underlying models. e.g. OpenRouter, LiteLLM-compatible, custom OpenAI-compatible.</summary>
    RouterAggregator = 3,
}
