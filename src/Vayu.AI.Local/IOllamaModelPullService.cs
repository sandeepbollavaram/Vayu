namespace Vayu.AI.Local;

/// <summary>
/// Streams an Ollama model pull, gated by <see cref="LocalModelCatalog"/>.
/// The interface deliberately accepts <see cref="IProgress{T}"/> rather than
/// returning a stream — UI binding stays simple and tests can capture
/// every emitted progress tick.
/// </summary>
public interface IOllamaModelPullService
{
    /// <summary>
    /// Performs an Ollama <c>POST /api/pull</c> for <paramref name="request"/>.
    /// </summary>
    /// <param name="request">Required — must reference a tag in <see cref="LocalModelCatalog"/>.</param>
    /// <param name="progress">Optional sink for streaming progress updates. Called on the calling thread (no marshalling).</param>
    /// <param name="cancellationToken">Cancels the in-flight pull. The final emitted progress will carry <see cref="OllamaModelPullProgress.IsCancelled"/> = true.</param>
    /// <returns>Final progress snapshot.</returns>
    /// <remarks>
    /// The service never installs Ollama itself, never spawns subprocesses,
    /// never makes cloud calls, and never logs secrets. Unknown tags are
    /// rejected before any HTTP request is made.
    /// </remarks>
    Task<OllamaModelPullProgress> PullModelAsync(
        OllamaModelPullRequest request,
        IProgress<OllamaModelPullProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
