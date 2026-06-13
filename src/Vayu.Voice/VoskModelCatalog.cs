using System.Collections.Immutable;

namespace Vayu.Voice;

/// <summary>
/// One downloadable Vosk model. Unlike a Whisper model (a single GGML file), a
/// Vosk model is a <em>directory</em> of files packaged as a ZIP archive; the
/// download service extracts it into the local wake/STT model folder.
/// </summary>
/// <param name="Id">Stable id, e.g. <c>"small-en-us"</c>.</param>
/// <param name="DisplayName">Human label for the UI.</param>
/// <param name="ArchiveFileName">The downloaded archive name, e.g. <c>vosk-model-small-en-us-0.15.zip</c>.</param>
/// <param name="DownloadUrl">Verified HTTPS source for the model ZIP.</param>
/// <param name="ApproxSizeMb">Approximate download size in MB (for the consent dialog).</param>
/// <param name="Note">Short guidance on when to pick this model.</param>
public sealed record VoskModelInfo(
    string Id,
    string DisplayName,
    string ArchiveFileName,
    string DownloadUrl,
    int ApproxSizeMb,
    string Note);

/// <summary>
/// A small, code-reviewed catalog of Vosk speech models Vayu can offer to
/// download for the local "Hey Vayu" wake word (and lightweight on-device STT).
/// URLs are the canonical models published at <c>alphacephei.com/vosk/models</c>.
/// Adding an entry is a deliberate change.
/// </summary>
/// <remarks>
/// Vayu never downloads a model automatically: a download always requires an
/// explicit user click and a consent dialog naming the source, size, and
/// destination. The model is large binary data, never a secret.
/// </remarks>
public static class VoskModelCatalog
{
    private const string BaseUrl = "https://alphacephei.com/vosk/models";

    /// <summary>All offered models, in recommended display order.</summary>
    public static ImmutableArray<VoskModelInfo> All { get; } =
    [
        new VoskModelInfo(
            Id: "small-en-us",
            DisplayName: "Vosk Small (English, US)",
            ArchiveFileName: "vosk-model-small-en-us-0.15.zip",
            DownloadUrl: $"{BaseUrl}/vosk-model-small-en-us-0.15.zip",
            ApproxSizeMb: 40,
            Note: "Recommended for the wake word. Small, fast, fully offline."),
    ];

    /// <summary>The model recommended by default for the wake word.</summary>
    public static VoskModelInfo Recommended => All[0]; // small-en-us

    /// <summary>Looks up a model by id; null when unknown.</summary>
    public static VoskModelInfo? TryGet(string id)
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
    /// Guards the download source: only the vetted Vosk model host over HTTPS is
    /// ever fetched, so a tampered catalog entry can't redirect a download
    /// elsewhere.
    /// </summary>
    public static bool IsAllowedDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host == "alphacephei.com"
            && uri.AbsolutePath.StartsWith("/vosk/models/", StringComparison.Ordinal);
    }
}
