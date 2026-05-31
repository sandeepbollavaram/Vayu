namespace Vayu.AI.Online;

/// <summary>
/// A redaction-safe description of an intended cloud call, shown to the user
/// before any private context leaves the device. Carries <b>no key</b> and no
/// raw prompt — only a summary the user can reason about.
/// </summary>
/// <param name="ProviderId">The provider that would receive the data, e.g. <c>gemini</c>.</param>
/// <param name="ProviderDisplayName">Human-facing provider name for the dialog title.</param>
/// <param name="Purpose">Why Vayu wants to call the cloud, e.g. "Plan your command".</param>
/// <param name="DataSummary">A short, non-sensitive summary of what would be sent (e.g. "your typed command text").</param>
/// <param name="EstimatedPromptChars">Approximate prompt size, so the user understands the scope.</param>
/// <param name="RequiresSensitiveContext">True when the request would include window/file/clipboard context beyond the bare command.</param>
/// <param name="CreatedAtUtc">When the request was created.</param>
public sealed record CloudConsentRequest(
    string ProviderId,
    string ProviderDisplayName,
    string Purpose,
    string DataSummary,
    int EstimatedPromptChars,
    bool RequiresSensitiveContext,
    DateTimeOffset CreatedAtUtc);
