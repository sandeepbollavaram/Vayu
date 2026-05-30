namespace Vayu.AI.Local;

/// <summary>
/// Lightweight description of an Ollama model on the user's machine.
/// Returned by <see cref="IOllamaRuntimeService.ListLocalModelsAsync"/>.
/// </summary>
/// <param name="Name">The model family name as Ollama reports it, e.g. <c>gemma3</c>.</param>
/// <param name="Tag">The full pullable tag, e.g. <c>gemma3:4b</c>.</param>
/// <param name="SizeBytes">On-disk size, if known. Null if Ollama did not report it.</param>
/// <param name="IsPresent">True if the model is already pulled and ready to use offline.</param>
public sealed record OllamaModelInfo(
    string Name,
    string Tag,
    long? SizeBytes,
    bool IsPresent);
