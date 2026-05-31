namespace Vayu.AI.Gemini;

/// <summary>
/// Resolves the Gemini API key across Vayu's secret-store chain. The returned
/// <see cref="GeminiKeyResolutionResult"/> is <c>internal</c>, so the key value
/// never leaves <c>Vayu.AI.Gemini</c>.
/// </summary>
internal interface IGeminiKeyResolver
{
    /// <summary>
    /// Resolves the key, or returns a NotConfigured result. Never throws for a
    /// missing key; never logs the value.
    /// </summary>
    Task<GeminiKeyResolutionResult> ResolveAsync(CancellationToken cancellationToken = default);
}
