namespace Vayu.Security;

/// <summary>
/// Cheap, local-only sanity checks on a secret value before Vayu trusts
/// it. Never calls a remote API — that's <c>SecretValidationService</c>'s
/// job in Milestone 3 with a redacted test call.
/// </summary>
public sealed class SecretValidationService
{
    private const int MinimumLength = 12;

    private static readonly HashSet<string> ExactPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "your-key-here",
        "your_key_here",
        "your-api-key",
        "your-api-key-here",
        "your_api_key",
        "your_api_key_here",
        "REPLACE_ME",
        "REPLACE-ME",
        "TODO",
        "todo",
        "changeme",
        "CHANGEME",
        "placeholder",
        "<your-key>",
        "<your-api-key>",
        "<your-api-key-here>",
    };

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> looks
    /// plausible enough to attempt using. The <paramref name="reason"/>
    /// out parameter carries a short, redaction-safe explanation for the
    /// rejection (or <c>"OK"</c> on success).
    /// </summary>
    public bool IsAcceptable(string? value, out string reason)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reason = "Empty or whitespace.";
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length < MinimumLength)
        {
            reason = $"Too short (minimum {MinimumLength} characters).";
            return false;
        }

        if (ExactPlaceholders.Contains(trimmed))
        {
            reason = "Looks like a placeholder value.";
            return false;
        }

        reason = "OK";
        return true;
    }
}
