using System.Collections.Immutable;

namespace Vayu.Voice;

/// <summary>
/// One downloadable Whisper model. The <see cref="DownloadUrl"/> points at the
/// canonical whisper.cpp GGML models, which the whisper.net runtime loads
/// directly.
/// </summary>
/// <param name="Id">Stable id, e.g. <c>"base.en"</c>.</param>
/// <param name="DisplayName">Human label for the UI.</param>
/// <param name="FileName">The on-disk file name, e.g. <c>ggml-base.en.bin</c>.</param>
/// <param name="DownloadUrl">Verified HTTPS source for the GGML model file.</param>
/// <param name="ApproxSizeMb">Approximate download size in MB (for the consent dialog).</param>
/// <param name="Note">Short guidance on when to pick this model.</param>
public sealed record WhisperModelInfo(
    string Id,
    string DisplayName,
    string FileName,
    string DownloadUrl,
    int ApproxSizeMb,
    string Note);

/// <summary>
/// A small, code-reviewed catalog of English Whisper models Vayu can offer to
/// download for local STT. URLs are the canonical
/// <c>ggerganov/whisper.cpp</c> GGML models on Hugging Face — the same files
/// the whisper.net runtime expects. Adding an entry is a deliberate change.
/// </summary>
/// <remarks>
/// Vayu never downloads a model automatically: a download always requires an
/// explicit user click and a consent dialog naming the source, size, and
/// destination. The model is large binary data, never a secret.
/// </remarks>
public static class WhisperModelCatalog
{
    private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";

    /// <summary>All offered models, in recommended display order.</summary>
    public static ImmutableArray<WhisperModelInfo> All { get; } =
    [
        new WhisperModelInfo(
            Id: "tiny.en",
            DisplayName: "Whisper Tiny (English)",
            FileName: "ggml-tiny.en.bin",
            DownloadUrl: $"{BaseUrl}/ggml-tiny.en.bin",
            ApproxSizeMb: 75,
            Note: "Fastest, lowest accuracy. Good for a quick test or weak hardware."),

        new WhisperModelInfo(
            Id: "base.en",
            DisplayName: "Whisper Base (English)",
            FileName: "ggml-base.en.bin",
            DownloadUrl: $"{BaseUrl}/ggml-base.en.bin",
            ApproxSizeMb: 142,
            Note: "Recommended. Good balance of speed and accuracy for commands."),

        new WhisperModelInfo(
            Id: "small.en",
            DisplayName: "Whisper Small (English)",
            FileName: "ggml-small.en.bin",
            DownloadUrl: $"{BaseUrl}/ggml-small.en.bin",
            ApproxSizeMb: 466,
            Note: "Better accuracy, slower. For stronger machines."),
    ];

    /// <summary>The model recommended by default.</summary>
    public static WhisperModelInfo Recommended => All[1]; // base.en

    /// <summary>Looks up a model by id; null when unknown.</summary>
    public static WhisperModelInfo? TryGet(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        foreach (var m in All)
        {
            if (string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return m;
            }
        }
        return null;
    }

    /// <summary>
    /// Guards the download source: only the vetted whisper.cpp Hugging Face host
    /// over HTTPS is ever fetched, so a tampered catalog entry can't redirect a
    /// download elsewhere.
    /// </summary>
    public static bool IsAllowedDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host == "huggingface.co"
            && uri.AbsolutePath.StartsWith("/ggerganov/whisper.cpp/resolve/", StringComparison.Ordinal);
    }
}
