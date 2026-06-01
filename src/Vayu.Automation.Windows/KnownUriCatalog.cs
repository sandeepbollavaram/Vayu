using System.Collections.Immutable;

namespace Vayu.Automation.Windows;

/// <summary>
/// Allowlist of URI schemes Vayu is permitted to hand to the OS shell when a
/// <see cref="KnownAppCatalog"/> entry uses <see cref="LaunchKind.Uri"/>.
/// </summary>
/// <remarks>
/// Store / MSIX apps (Spotify, WhatsApp, …) frequently register a protocol
/// handler but place no classic <c>.lnk</c> on the Desktop or Start Menu, so
/// the shortcut scan in <see cref="InstalledAppCatalog"/> cannot find them.
/// Launching their registered scheme is the reliable way to open them.
/// <para>
/// This catalog is the safety boundary: only a vetted, code-reviewed scheme
/// from <see cref="AllowedSchemes"/> may be launched, and the launch target
/// must be the <em>bare scheme</em> (e.g. <c>"spotify:"</c>) with no payload.
/// Vayu never builds a URI from user text and never launches an arbitrary
/// scheme — that would let a crafted command run a registered handler with an
/// attacker-controlled argument.
/// </para>
/// </remarks>
public static class KnownUriCatalog
{
    /// <summary>
    /// Schemes Vayu may launch, without the trailing colon. Lowercase.
    /// Adding one is a deliberate, reviewable code change.
    /// </summary>
    public static ImmutableHashSet<string> AllowedSchemes { get; } =
        ImmutableHashSet.Create(
            StringComparer.OrdinalIgnoreCase,
            "spotify",
            "whatsapp",
            "ms-settings");

    /// <summary>
    /// Returns <see langword="true"/> only when <paramref name="target"/> is a
    /// safe, bare, allowlisted launch URI: a single allowed scheme followed by
    /// a colon and nothing else (apart from an optional <c>//</c>). Anything
    /// with a payload, query, whitespace, control characters, or an unknown
    /// scheme is rejected.
    /// </summary>
    public static bool IsAllowedLaunchUri(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        // No whitespace or control characters anywhere — a launch URI is a
        // single token. This blocks "spotify: && calc" style injection attempts.
        foreach (var c in target)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                return false;
            }
        }

        var colon = target.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        var scheme = target[..colon];
        if (!AllowedSchemes.Contains(scheme))
        {
            return false;
        }

        // Everything after the scheme must be empty or just the "//" authority
        // marker — we launch the app, we do not pass it a payload.
        var rest = target[(colon + 1)..];
        return rest.Length == 0 || rest == "//";
    }
}
