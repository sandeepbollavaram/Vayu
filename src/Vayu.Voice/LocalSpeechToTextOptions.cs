namespace Vayu.Voice;

/// <summary>
/// Configuration for the local speech-to-text path. Local STT is <b>off by
/// default</b> until a provider + model are configured. Holds no secret and no
/// audio.
/// </summary>
public sealed record LocalSpeechToTextOptions
{
    /// <summary>Stable id used in results/logs, e.g. <c>"local-stt"</c>.</summary>
    public string ProviderName { get; init; } = "local-stt";

    /// <summary>Master switch. Default <see langword="false"/> — local STT is opt-in.</summary>
    public bool EnableLocalStt { get; init; }

    /// <summary>Which local provider to prefer, e.g. <c>"whispercpp"</c>.</summary>
    public string PreferredProvider { get; init; } = "whispercpp";

    /// <summary>Path to the local model file (e.g. a Whisper GGUF). Null until the user configures one.</summary>
    public string? ModelPath { get; init; }

    /// <summary>Hard cap on a single push-to-talk capture, in seconds. Default 30.</summary>
    public int MaxCaptureSeconds { get; init; } = 30;
}
