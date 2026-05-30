namespace Vayu.Core.Ai;

/// <summary>
/// Static metadata about an AI provider Vayu knows about. The registry of
/// these descriptors is the source of truth for the First Run Setup Wizard,
/// the Settings page, and any future provider connectors.
/// </summary>
/// <param name="Id">Stable kebab-case identifier, e.g. <c>"ollama"</c>, <c>"gemini"</c>, <c>"openrouter"</c>. Used in secret store names (<c>Vayu:&lt;Id&gt;:ApiKey</c>) and persisted in <c>UserSettings</c>.</param>
/// <param name="DisplayName">Human-readable label for the picker, e.g. <c>"Google Gemini"</c>.</param>
/// <param name="Kind">Provider category (offline, online direct, cloud platform, router).</param>
/// <param name="RequiresApiKey">True when the provider needs an API key (or analogous credential bundle). Local providers like Ollama set this to false.</param>
/// <param name="SetupHelpUrl">Optional. URL the wizard opens for "How do I get a key?". Null for providers that need no key, or providers that need bespoke onboarding handled by Vayu (e.g. custom OpenAI-compatible).</param>
/// <param name="Notes">Optional human-readable notes shown next to the picker entry. Should not contain links — use Markdown elsewhere.</param>
public sealed record AiProviderDescriptor(
    string Id,
    string DisplayName,
    AiProviderKind Kind,
    bool RequiresApiKey,
    string? SetupHelpUrl = null,
    string? Notes = null);
