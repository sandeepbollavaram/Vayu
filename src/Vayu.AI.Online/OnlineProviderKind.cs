namespace Vayu.AI.Online;

/// <summary>
/// How an online provider is reached. Drives setup UX and which transport a
/// future connector uses — it does not change the safety model (every kind is
/// off by default and consent-gated).
/// </summary>
public enum OnlineProviderKind
{
    /// <summary>A first-party REST API with its own auth, e.g. Gemini, OpenAI, Anthropic.</summary>
    DirectApi = 0,

    /// <summary>An aggregator that fronts many models behind one key, e.g. OpenRouter.</summary>
    Router = 1,

    /// <summary>A managed cloud platform endpoint, e.g. Azure OpenAI or AWS Bedrock.</summary>
    CloudPlatform = 2,

    /// <summary>A user-supplied OpenAI-compatible endpoint (self-hosted gateways, LiteLLM, etc.).</summary>
    CustomOpenAiCompatible = 3,
}
