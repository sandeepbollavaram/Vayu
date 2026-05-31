namespace Vayu.AI.Online;

/// <summary>
/// Pure, secret-free projection of one <see cref="OnlineProviderDescriptor"/>
/// for the Settings Provider Registry grid. Carries no key and no secret —
/// only display text and status flags.
/// </summary>
/// <param name="ProviderId">Stable id, e.g. <c>gemini</c>.</param>
/// <param name="DisplayName">Human-facing name.</param>
/// <param name="KindLabel">Provider kind as text, e.g. "Direct API".</param>
/// <param name="CapabilityLabels">Capability chips, e.g. ["Chat", "JSON mode", "Streaming"].</param>
/// <param name="StatusLabel">"Available now" or "Planned", plus configured state for the working provider.</param>
/// <param name="IsAvailableNow">True for providers with a working connector (Gemini today).</param>
/// <param name="IsConfigured">True when a key is configured (only meaningful for an available provider).</param>
/// <param name="ActionLabel">Button label, e.g. "Configured", "Set up below", or "Coming in M3.7+".</param>
/// <param name="IsActionEnabled">Whether the card's action is actionable (false for planned providers).</param>
public sealed record ProviderCardViewModel(
    string ProviderId,
    string DisplayName,
    string KindLabel,
    IReadOnlyList<string> CapabilityLabels,
    string StatusLabel,
    bool IsAvailableNow,
    bool IsConfigured,
    string ActionLabel,
    bool IsActionEnabled);
