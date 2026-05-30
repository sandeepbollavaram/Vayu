namespace Vayu.AI.Gemini;

/// <summary>
/// Handles the Gemini API key lifecycle from the user's perspective:
/// save (to Windows Credential Manager by default), check presence,
/// validate, revoke. M1 ships only the contract; the real save +
/// validate (HTTP) lands in Milestone 3.
/// </summary>
/// <remarks>
/// Implementations must never:
/// <list type="bullet">
/// <item>persist the key to <c>appsettings.json</c>, <c>config.json</c>, or any tracked file;</item>
/// <item>log the key, even at Debug level;</item>
/// <item>return the key from any method (only presence/validity is exposed).</item>
/// </list>
/// </remarks>
public interface IGeminiKeySetupService
{
    /// <summary>
    /// True if a Gemini key is resolvable from any of the secret stores
    /// (Windows Credential Manager, <c>GEMINI_API_KEY</c> env var, or DPAPI
    /// fallback file). Does NOT return the value.
    /// </summary>
    Task<bool> IsKeyConfiguredAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the user-supplied key into the highest-priority writeable
    /// secret store (Windows Credential Manager). The argument must be
    /// discarded by the caller immediately after this returns; the textbox
    /// that produced it must be cleared.
    /// </summary>
    Task SaveKeyAsync(string apiKey, CancellationToken ct = default);

    /// <summary>
    /// Deletes the key entry from the secret store and clears any
    /// in-memory cache. Safe to call when no key is configured.
    /// </summary>
    Task RemoveKeyAsync(CancellationToken ct = default);

    /// <summary>
    /// Performs a redacted test call (no prompt content, no headers logged)
    /// to confirm the configured key is accepted by Gemini. Returns false
    /// for malformed or revoked keys. Never throws on auth failure.
    /// </summary>
    Task<bool> ValidateKeyAsync(CancellationToken ct = default);
}
