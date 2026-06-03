using System.Collections.Immutable;

namespace Vayu.Voice;

/// <summary>
/// Configuration for the local "Hey Vayu" wake word. Off by default; fully
/// on-device (no cloud wake detection); holds no secret and no audio.
/// </summary>
public sealed record WakeWordOptions
{
    /// <summary>Master switch. Default <see langword="false"/> — wake word is opt-in.</summary>
    public bool EnableWakeWord { get; init; }

    /// <summary>Path to the local wake/STT model directory (e.g. a Vosk model). Null until configured.</summary>
    public string? ModelPath { get; init; }

    /// <summary>Phrases that trigger Vayu. Matching is case-insensitive.</summary>
    public ImmutableArray<string> Phrases { get; init; } =
        ImmutableArray.Create("hey vayu", "vayu");

    /// <summary>Cooldown after a trigger before re-arming, in milliseconds.</summary>
    public int CooldownMs { get; init; } = 1500;

    /// <summary>Provider id surfaced in status, e.g. <c>"vosk"</c>.</summary>
    public string ProviderName { get; init; } = "vosk";
}
