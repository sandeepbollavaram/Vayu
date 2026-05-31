namespace Vayu.AI.Online;

/// <summary>
/// Static, secret-free description of an online provider Vayu can offer in
/// the First Run Wizard and Settings. A descriptor is pure data — it never
/// holds a key and never makes a call.
/// </summary>
/// <param name="ProviderId">Stable lowercase id, e.g. <c>gemini</c>. Used in audit logs and key lookups.</param>
/// <param name="DisplayName">Human-facing name, e.g. <c>Google Gemini</c>.</param>
/// <param name="Kind">How the provider is reached.</param>
/// <param name="Description">One-line summary for the picker.</param>
/// <param name="SetupDocPath">Repo-relative doc that walks the user through getting a key, e.g. <c>docs/GEMINI_SETUP.md</c>.</param>
/// <param name="SupportsChat">True when the provider exposes a chat/completions surface.</param>
/// <param name="SupportsJsonMode">True when the provider can be asked for strict JSON output (needed for planning).</param>
/// <param name="SupportsStreaming">True when token streaming is available.</param>
/// <param name="IsEnabledByDefault">Always <see langword="false"/> — no provider is on by default.</param>
public sealed record OnlineProviderDescriptor(
    string ProviderId,
    string DisplayName,
    OnlineProviderKind Kind,
    string Description,
    string SetupDocPath,
    bool SupportsChat,
    bool SupportsJsonMode,
    bool SupportsStreaming,
    bool IsEnabledByDefault = false);
