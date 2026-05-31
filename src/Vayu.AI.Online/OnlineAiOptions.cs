namespace Vayu.AI.Online;

/// <summary>
/// Top-level configuration for the optional online (cloud) AI layer. Cloud
/// AI is <b>off by default</b>; nothing here holds a secret.
/// </summary>
/// <remarks>
/// Mirrors the safety posture of <c>LocalAiOptions</c>: the user must opt in
/// before any cloud provider is contacted, and must consent before private
/// context leaves the device. M3.1 ships this as a contract only — no
/// connector reads it for real HTTP until M3.2+.
/// </remarks>
public sealed record OnlineAiOptions
{
    /// <summary>Master switch for cloud AI. Default <see langword="false"/>.</summary>
    public bool EnableOnlineAi { get; init; }

    /// <summary>Hybrid routing (local first, cloud fallback). Default <see langword="false"/> — lands in M3.5.</summary>
    public bool EnableHybridMode { get; init; }

    /// <summary>The provider id used when online AI is enabled, e.g. <c>"gemini"</c>. Empty until the user configures one.</summary>
    public string? DefaultProviderId { get; init; }

    /// <summary>When <see langword="true"/> (the default), Vayu asks for explicit consent before sending private context to any cloud provider.</summary>
    public bool RequireConsentBeforeCloudCall { get; init; } = true;

    /// <summary>Per-call timeout in seconds. Default 30.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Hard cap on prompt length sent to a cloud provider. Default 8000.</summary>
    public int MaxPromptChars { get; init; } = 8000;
}
