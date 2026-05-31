namespace Vayu.AI.Local;

/// <summary>
/// Caller-controlled parameters for an Ollama <c>/api/pull</c> request.
/// All side-effecting fields are explicit — there is no "default model".
/// </summary>
/// <param name="ModelTag">The exact Ollama tag to pull, e.g. <c>gemma3:4b</c>. Must exist in <see cref="LocalModelCatalog"/>.</param>
/// <param name="StreamProgress">When <see langword="true"/>, the service streams JSON-line progress events. <see langword="false"/> issues a single non-streaming request.</param>
public sealed record OllamaModelPullRequest(string ModelTag, bool StreamProgress = true);
