using System.Text.RegularExpressions;

namespace Vayu.Security;

/// <summary>
/// Replaces secret-shaped substrings with <see cref="Replacement"/>. Run on
/// every log line, audit field, and exception message before any sink sees
/// the text.
/// </summary>
/// <remarks>
/// Patterns are intentionally broad. False positives are acceptable —
/// false negatives are not. New patterns must arrive with a test in
/// <c>Vayu.Security.Tests</c>.
/// </remarks>
public static class SecretRedactor
{
    /// <summary>The string that replaces every matched secret.</summary>
    public const string Replacement = "[REDACTED]";

    private static readonly Regex[] TokenPatterns =
    {
        // Google / Gemini API keys
        new(@"AIza[0-9A-Za-z\-_]{30,}", RegexOptions.Compiled),
        // OpenAI / Anthropic style
        new(@"sk-[A-Za-z0-9\-_]{20,}", RegexOptions.Compiled),
        // GitHub classic personal access token
        new(@"ghp_[A-Za-z0-9]{30,}", RegexOptions.Compiled),
        // GitHub fine-grained personal access token
        new(@"github_pat_[A-Za-z0-9_]{40,}", RegexOptions.Compiled),
        // GitHub OAuth / server / refresh
        new(@"gh[osr]_[A-Za-z0-9]{30,}", RegexOptions.Compiled),
        // Slack bot / user / app tokens
        new(@"xox[abprs]-[A-Za-z0-9\-]{10,}", RegexOptions.Compiled),
        // JWT (three base64url segments)
        new(@"eyJ[A-Za-z0-9_\-]+\.eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled),
        // PEM private key blocks (multiline)
        new(@"-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----[\s\S]+?-----END (?:[A-Z]+ )?PRIVATE KEY-----",
            RegexOptions.Compiled),
    };

    // Bearer token: capture the token after the prefix, preserve the prefix.
    private static readonly Regex BearerPattern =
        new(@"(?i)\bBearer\s+(?<val>[A-Za-z0-9._\-]{20,})", RegexOptions.Compiled);

    // Generic key=value or key: value forms.
    private static readonly Regex KeyValuePattern =
        new(@"(?<key>(?i:api[_-]?key|access[_-]?key|secret[_-]?key|token|secret|password|passwd))\s*[=:]\s*(?<val>[^\s,;""'`]+)",
            RegexOptions.Compiled);

    // HTTP-style auth headers.
    private static readonly Regex HeaderPattern =
        new(@"(?<hdr>(?i:Authorization|X-Api-Key|X-Auth-Token))\s*:\s*(?<val>[^\r\n]+)",
            RegexOptions.Compiled);

    /// <summary>
    /// Returns <paramref name="input"/> with every secret-shaped substring
    /// replaced by <see cref="Replacement"/>. Returns the empty string when
    /// <paramref name="input"/> is <see langword="null"/>.
    /// </summary>
    public static string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var s = input;

        foreach (var p in TokenPatterns)
        {
            s = p.Replace(s, Replacement);
        }

        s = BearerPattern.Replace(s, m => m.Value.Replace(m.Groups["val"].Value, Replacement));
        s = HeaderPattern.Replace(s, m => $"{m.Groups["hdr"].Value}: {Replacement}");
        s = KeyValuePattern.Replace(s, m => $"{m.Groups["key"].Value}={Replacement}");

        return s;
    }
}
