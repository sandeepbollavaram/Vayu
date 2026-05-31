namespace Vayu.AI.Local;

/// <summary>
/// Lightweight description of an Ollama model on the user's machine.
/// Returned by <see cref="IOllamaRuntimeService.ListLocalModelsAsync"/>.
/// </summary>
/// <param name="Name">The model family name as Ollama reports it, e.g. <c>gemma3</c>.</param>
/// <param name="Tag">The full pullable tag, e.g. <c>gemma3:4b</c>.</param>
/// <param name="SizeBytes">On-disk size, if known. Null if Ollama did not report it.</param>
/// <param name="IsPresent">True if the model is already pulled and ready to use offline.</param>
/// <remarks>
/// M2.4 added optional metadata fields parsed from Ollama's <c>/api/tags</c> <c>details</c>
/// block. They are <see langword="init"/>-only so existing callers that use the
/// positional constructor are unaffected. Every field tolerates absence and
/// must never be relied on for safety-critical decisions — Installed/Missing
/// matching uses <see cref="Tag"/> only.
/// </remarks>
public sealed record OllamaModelInfo(
    string Name,
    string Tag,
    long? SizeBytes,
    bool IsPresent)
{
    /// <summary>Content-addressable digest reported by Ollama, e.g. <c>sha256:…</c>. Null when absent.</summary>
    public string? Digest { get; init; }

    /// <summary>Last-modified UTC timestamp from <c>/api/tags</c>, if parsable. Null when absent or malformed.</summary>
    public DateTimeOffset? ModifiedAtUtc { get; init; }

    /// <summary>Model family as reported by <c>details.family</c>, e.g. <c>gemma3</c>. Falls back to <see cref="Name"/>'s family when absent.</summary>
    public string? Family { get; init; }

    /// <summary>Parameter-size label from <c>details.parameter_size</c>, e.g. <c>4B</c>. Null when absent.</summary>
    public string? ParameterSize { get; init; }

    /// <summary>Quantization label from <c>details.quantization_level</c>, e.g. <c>Q4_K_M</c>. Null when absent.</summary>
    public string? QuantizationLevel { get; init; }

    /// <summary>
    /// Human-readable size string (e.g. <c>"3.6 GB"</c>) computed from <see cref="SizeBytes"/>.
    /// Returns <see cref="string.Empty"/> when the size is unknown.
    /// </summary>
    public string DisplaySize => FormatSize(SizeBytes);

    /// <summary>
    /// Pure size formatter exposed for the UI layer. Returns <c>"3.6 GB"</c> for values ≥ 1 GiB,
    /// <c>"812 MB"</c> for values ≥ 1 MiB, <c>"45 KB"</c> for the rest, and an empty string when
    /// <paramref name="sizeBytes"/> is null or non-positive.
    /// </summary>
    public static string FormatSize(long? sizeBytes)
    {
        if (sizeBytes is null || sizeBytes.Value <= 0)
        {
            return string.Empty;
        }

        const double KiB = 1024d;
        const double MiB = KiB * 1024d;
        const double GiB = MiB * 1024d;

        var bytes = (double)sizeBytes.Value;
        if (bytes >= GiB)
        {
            return $"{bytes / GiB:0.0} GB";
        }
        if (bytes >= MiB)
        {
            return $"{bytes / MiB:0} MB";
        }
        return $"{bytes / KiB:0} KB";
    }
}
