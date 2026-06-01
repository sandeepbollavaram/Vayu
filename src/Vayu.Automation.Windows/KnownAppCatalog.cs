using System.Collections.Immutable;

namespace Vayu.Automation.Windows;

/// <summary>
/// Fixed M1 catalog of safe launch targets. Vayu only launches things
/// in this catalog. Adding a new entry is a deliberate code change
/// (and a PR review opportunity) — there is no user-facing "register an
/// app" flow in M1. User shortcuts arrive in M6 via the
/// <c>AppShortcuts</c> table.
/// </summary>
public static class KnownAppCatalog
{
    /// <summary>All registered apps, in display order.</summary>
    public static IReadOnlyList<KnownAppInfo> All { get; } = BuildAll();

    private static readonly ImmutableDictionary<string, KnownAppInfo> ByKey = BuildLookup(All);

    /// <summary>
    /// Looks up an entry by <paramref name="idOrAlias"/>. Case-insensitive;
    /// recognises aliases like <c>"vs code"</c> → <c>vscode</c>.
    /// Returns <see langword="null"/> when no match.
    /// </summary>
    public static KnownAppInfo? TryGet(string idOrAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrAlias);
        var key = NormalizeKey(idOrAlias);
        return ByKey.TryGetValue(key, out var info) ? info : null;
    }

    private static string NormalizeKey(string input)
    {
        var trimmed = input.Trim().ToLowerInvariant();
        // Collapse internal whitespace so "vs code" and "vs   code" both match.
        return string.Concat(trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<KnownAppInfo> BuildAll() =>
    [
        new KnownAppInfo("chrome",     "Google Chrome",      "chrome",                      LaunchKind.Executable),
        new KnownAppInfo("edge",       "Microsoft Edge",     "msedge",                      LaunchKind.Executable),
        new KnownAppInfo("vscode",     "Visual Studio Code", "code",                        LaunchKind.Executable),
        new KnownAppInfo("notepad",    "Notepad",            "notepad",                     LaunchKind.Executable),
        new KnownAppInfo("terminal",   "Windows Terminal",   "wt",                          LaunchKind.Executable),
        new KnownAppInfo("calculator", "Calculator",         "calc",                        LaunchKind.Executable),
        new KnownAppInfo("explorer",   "File Explorer",      "explorer",                    LaunchKind.Executable),
        new KnownAppInfo("downloads",  "Downloads folder",   @"%USERPROFILE%\Downloads",    LaunchKind.Folder),

        // URI-launched apps. These are typically Store/MSIX or system apps that
        // register a protocol handler but have no classic .lnk on the Desktop or
        // Start Menu, so the shortcut scan misses them. Each LaunchTarget is a
        // registered URI scheme vetted in KnownUriCatalog — never arbitrary user
        // text. Launching the bare scheme (e.g. "spotify:") activates the app
        // without a payload.
        new KnownAppInfo("spotify",    "Spotify",            "spotify:",                    LaunchKind.Uri),
        new KnownAppInfo("whatsapp",   "WhatsApp",           "whatsapp:",                   LaunchKind.Uri),
        new KnownAppInfo("settings",   "Windows Settings",   "ms-settings:",                LaunchKind.Uri),
    ];

    private static ImmutableDictionary<string, KnownAppInfo> BuildLookup(IReadOnlyList<KnownAppInfo> all)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, KnownAppInfo>(StringComparer.Ordinal);

        // Canonical AppIds.
        foreach (var info in all)
        {
            builder.Add(info.AppId, info);
        }

        // Friendly aliases — never collide with a canonical id.
        AddAlias(builder, "vscode",   "vscode");
        AddAlias(builder, "visualstudiocode", "vscode");
        AddAlias(builder, "windowsterminal", "terminal");
        AddAlias(builder, "wt", "terminal");
        AddAlias(builder, "googlechrome", "chrome");
        AddAlias(builder, "microsoftedge", "edge");
        AddAlias(builder, "spotifymusic", "spotify");
        AddAlias(builder, "whatsappdesktop", "whatsapp");
        AddAlias(builder, "calc", "calculator");
        AddAlias(builder, "fileexplorer", "explorer");
        AddAlias(builder, "files", "explorer");
        AddAlias(builder, "windowssettings", "settings");
        AddAlias(builder, "settingsapp", "settings");

        return builder.ToImmutable();

        static void AddAlias(ImmutableDictionary<string, KnownAppInfo>.Builder b, string aliasNormalized, string canonicalId)
        {
            var canonical = b[canonicalId];
            if (!b.ContainsKey(aliasNormalized))
            {
                b.Add(aliasNormalized, canonical);
            }
        }
    }
}
