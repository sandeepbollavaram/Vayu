using System.Runtime.Versioning;

using Microsoft.Win32;

namespace Vayu.Automation.Windows;

/// <summary>
/// Resolves an app name to a launchable <em>executable path</em> using the
/// Windows <c>App Paths</c> registry key
/// (<c>SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths</c>) under both
/// <c>HKCU</c> and <c>HKLM</c>. Many normal desktop installs (Spotify's classic
/// installer, Chrome, etc.) register here even when they leave no Desktop or
/// Start-Menu shortcut, so this widens discovery without guessing paths.
/// </summary>
/// <remarks>
/// Safety boundary:
/// <list type="bullet">
/// <item>Only the <em>default value</em> of an <c>App Paths\&lt;exe&gt;</c> key
/// is read — that value is an executable path, never a command line with
/// arguments. Vayu never executes a registry command string.</item>
/// <item>The resolved value must exist on disk and end in <c>.exe</c>; anything
/// else (a URL handler, a quoted command with switches, a missing file) is
/// rejected so a crafted/poisoned registry entry cannot smuggle arguments.</item>
/// <item>Lookup keys are matched against an internal, code-reviewed map of app
/// id → candidate executable names — Vayu never reads an arbitrary registry key
/// built from user text.</item>
/// </list>
/// </remarks>
public interface IAppPathResolver
{
    /// <summary>
    /// Returns a validated executable path for <paramref name="appId"/>, or
    /// <see langword="null"/> when nothing safe is found. Implementations must
    /// never return a command line or a non-executable.
    /// </summary>
    string? TryResolveExecutable(string appId);
}

/// <inheritdoc cref="IAppPathResolver"/>
public sealed class WindowsAppPathsResolver : IAppPathResolver
{
    private const string AppPathsSubKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

    /// <summary>
    /// App id → the executable file names to probe under <c>App Paths</c>.
    /// Code-reviewed; never derived from user input.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> CandidateExecutables =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["spotify"] = ["Spotify.exe"],
            ["chrome"]  = ["chrome.exe"],
            ["edge"]    = ["msedge.exe"],
            ["vscode"]  = ["code.exe", "Code.exe"],
            ["whatsapp"] = ["WhatsApp.exe"],
        };

    /// <summary>
    /// Returns a validated executable path for <paramref name="appId"/> from the
    /// App Paths registry, or <see langword="null"/> when nothing safe is found.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public string? TryResolveExecutable(string appId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        if (!CandidateExecutables.TryGetValue(appId.Trim(), out var exeNames))
        {
            return null;
        }

        foreach (var exe in exeNames)
        {
            var fromUser = ReadDefaultValue(Registry.CurrentUser, exe);
            if (IsSafeExecutablePath(fromUser))
            {
                return fromUser;
            }

            var fromMachine = ReadDefaultValue(Registry.LocalMachine, exe);
            if (IsSafeExecutablePath(fromMachine))
            {
                return fromMachine;
            }
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadDefaultValue(RegistryKey hive, string exeName)
    {
        try
        {
            using var key = hive.OpenSubKey($@"{AppPathsSubKey}\{exeName}");
            return key?.GetValue(null) as string;
        }
#pragma warning disable CA1031 // A bad/locked registry entry must never crash discovery.
        catch (Exception)
        {
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// True only when <paramref name="value"/> is a plain, existing
    /// <c>.exe</c> path with no embedded arguments. This is what keeps a
    /// poisoned registry value (a command line, a non-exe, a switch) from ever
    /// being launched.
    /// </summary>
    public static bool IsSafeExecutablePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // App Paths default values are sometimes quoted; accept a single
        // surrounding pair of quotes, but nothing that looks like a command line.
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        // No second quote, no argument separators — a bare path only.
        if (trimmed.Contains('"', StringComparison.Ordinal))
        {
            return false;
        }

        if (!trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return File.Exists(trimmed);
        }
#pragma warning disable CA1031 // Path probing must never throw into the caller.
        catch (Exception)
        {
            return false;
        }
#pragma warning restore CA1031
    }
}
