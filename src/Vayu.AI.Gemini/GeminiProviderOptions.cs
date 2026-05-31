namespace Vayu.AI.Gemini;

/// <summary>
/// Configuration for the Gemini connector. <b>Holds no API key</b> — the key
/// is resolved at call time from the secret stores and never stored here.
/// </summary>
public sealed record GeminiProviderOptions
{
    /// <summary>Stable provider id, matching the online catalog entry.</summary>
    public string ProviderId { get; init; } = "gemini";

    /// <summary>
    /// Base of the Gemini <c>generateContent</c> REST endpoint. The model and
    /// <c>:generateContent</c> verb are appended by the connector.
    /// </summary>
    public Uri Endpoint { get; init; } = new Uri("https://generativelanguage.googleapis.com/v1beta/models/");

    /// <summary>Model name used for planning. A small, widely-available Gemini model by default.</summary>
    public string ModelName { get; init; } = "gemini-2.0-flash";

    /// <summary>Environment variable Vayu reads for the Gemini key (matches <c>SecureConfigService</c>).</summary>
    public string ApiKeyEnvironmentVariable { get; init; } = "GEMINI_API_KEY";

    /// <summary>Per-call timeout in seconds. Default 30.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Hard cap on prompt length sent to Gemini. Default 8000.</summary>
    public int MaxPromptChars { get; init; } = 8000;

    /// <summary>Confidence floor — a plan below this is rejected. Default 0.60.</summary>
    public double MinimumConfidence { get; init; } = 0.60;
}
