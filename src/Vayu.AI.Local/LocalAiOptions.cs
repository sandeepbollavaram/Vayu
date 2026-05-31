namespace Vayu.AI.Local;

/// <summary>
/// Top-level configuration for Vayu's offline AI layer. Provider-specific
/// options (e.g. Ollama executable path) live alongside their providers
/// and arrive in M2.2 onwards. The defaults here are deliberately safe:
/// the local AI is <b>disabled</b> until the user opts in via the First
/// Run Setup Wizard.
/// </summary>
public sealed record LocalAiOptions
{
    /// <summary>HTTP endpoint of the local AI runtime (Ollama by default).</summary>
    public Uri Endpoint { get; init; } = new Uri("http://localhost:11434");

    /// <summary>Tag of the model Vayu plans against by default. Matches <see cref="LocalModelCatalog.Recommended"/>.</summary>
    public string DefaultModel { get; init; } = "gemma3:4b";

    /// <summary>Per-request timeout for any local AI call. 30 s is enough for a small model on modern hardware.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Off by default so Vayu does not probe the local runtime or
    /// instantiate any provider until the user has finished the First
    /// Run Setup Wizard (M2.5) and consented to enable offline AI.
    /// </summary>
    public bool EnableLocalAi { get; init; }
}
