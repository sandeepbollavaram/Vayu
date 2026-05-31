namespace Vayu.AI.Local;

/// <summary>
/// One progress tick reported by <see cref="IOllamaModelPullService.PullModelAsync"/>.
/// </summary>
/// <param name="ModelTag">The tag being pulled.</param>
/// <param name="Status">Free-form status string from Ollama (e.g. <c>"pulling manifest"</c>, <c>"downloading"</c>, <c>"verifying sha256 digest"</c>, <c>"success"</c>).</param>
/// <param name="CompletedBytes">Bytes transferred for the current layer, when reported. <see langword="null"/> for non-byte progress lines.</param>
/// <param name="TotalBytes">Layer total, when reported.</param>
/// <param name="IsComplete">True when Ollama reported <c>success</c> or the stream finished without error.</param>
/// <param name="IsCancelled">True when the caller cancelled mid-stream.</param>
/// <param name="ErrorMessage">Redaction-safe error text, if the pull failed. <see langword="null"/> on success or cancellation.</param>
public sealed record OllamaModelPullProgress(
    string ModelTag,
    string Status,
    long? CompletedBytes,
    long? TotalBytes,
    bool IsComplete,
    bool IsCancelled = false,
    string? ErrorMessage = null)
{
    /// <summary>0-100 percentage when both <see cref="CompletedBytes"/> and <see cref="TotalBytes"/> are known, else <see langword="null"/>.</summary>
    public double? Percent
        => CompletedBytes is { } c && TotalBytes is { } t && t > 0
            ? Math.Clamp((double)c / t * 100.0, 0.0, 100.0)
            : null;

    /// <summary>UI-friendly compact summary string. Never throws.</summary>
    public string DisplayText
    {
        get
        {
            if (IsCancelled)
            {
                return $"Cancelled — {Status}";
            }
            if (ErrorMessage is not null)
            {
                return $"Error — {ErrorMessage}";
            }
            var pct = Percent;
            if (pct is not null)
            {
                return $"{Status} · {pct.Value:0}%";
            }
            return Status;
        }
    }
}
