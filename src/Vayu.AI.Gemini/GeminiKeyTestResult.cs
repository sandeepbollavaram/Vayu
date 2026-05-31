namespace Vayu.AI.Gemini;

/// <summary>
/// Outcome of <see cref="GeminiProvider.TestKeyAsync"/>. Carries only a
/// success flag and a short, redaction-safe message — never the key, never
/// the request/response body.
/// </summary>
/// <param name="Success">True when Gemini accepted the minimal health-check request.</param>
/// <param name="Message">A short user-facing message (e.g. "Test succeeded." / "Test failed: invalid or revoked key.").</param>
public sealed record GeminiKeyTestResult(bool Success, string Message)
{
    public static GeminiKeyTestResult Ok(string message = "Test succeeded.") => new(true, message);

    public static GeminiKeyTestResult Failed(string message) => new(false, message);
}
