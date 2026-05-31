using System.Text;

namespace Vayu.Automation.Windows;

/// <summary>
/// Discovers shortcut entries on the user's Desktop and Start Menu so
/// commands like <c>open spotify</c> can resolve to a real launchable app
/// without hardcoding machine-specific paths. Read-only: this catalog
/// never modifies the file system and never launches anything itself —
/// it returns <see cref="InstalledAppEntry"/> objects that the launcher
/// then opens via the OS shell.
/// </summary>
/// <remarks>
/// Default scan roots:
/// <list type="bullet">
/// <item><c>%USERPROFILE%\Desktop</c></item>
/// <item><c>%PUBLIC%\Desktop</c></item>
/// <item><c>%APPDATA%\Microsoft\Windows\Start Menu\Programs</c></item>
/// <item><c>%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs</c></item>
/// </list>
/// Tests pass their own (temp-folder) roots through the secondary
/// constructor for hermetic scans.
/// </remarks>
public sealed class InstalledAppCatalog
{
    private static readonly string[] DefaultExtensions = { ".lnk", ".url" };
    private const int DefaultMaxRecursionDepth = 4;
    private const int DefaultMaxMatches = 16;

    private readonly IReadOnlyList<(string Path, string Source)> _roots;
    private readonly HashSet<string> _extensions;
    private readonly int _maxMatches;
    private readonly int _maxDepth;

    /// <summary>Uses the default Windows scan roots above.</summary>
    public InstalledAppCatalog()
        : this(DefaultRoots(), DefaultExtensions, DefaultMaxMatches, DefaultMaxRecursionDepth)
    {
    }

    /// <summary>Test-friendly constructor. Pass any list of roots and extensions.</summary>
    public InstalledAppCatalog(
        IReadOnlyList<(string Path, string Source)> roots,
        IReadOnlyList<string>? extensions = null,
        int maxMatches = DefaultMaxMatches,
        int maxRecursionDepth = DefaultMaxRecursionDepth)
    {
        ArgumentNullException.ThrowIfNull(roots);
        _roots = roots;
        _extensions = new HashSet<string>(
            extensions ?? DefaultExtensions,
            StringComparer.OrdinalIgnoreCase);
        _maxMatches = Math.Max(1, maxMatches);
        _maxDepth = Math.Max(1, maxRecursionDepth);
    }

    /// <summary>
    /// Returns shortcut entries whose normalised display name contains the
    /// normalised <paramref name="query"/>. Case-insensitive; punctuation
    /// and whitespace are ignored, so <c>"vs code"</c> matches
    /// <c>"Visual Studio Code"</c> via the surrounding agent's lookup.
    /// </summary>
    public Task<IReadOnlyList<InstalledAppEntry>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var normalised = Normalize(query);
        if (normalised.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<InstalledAppEntry>>(Array.Empty<InstalledAppEntry>());
        }

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MaxRecursionDepth = _maxDepth,
            ReturnSpecialDirectories = false,
        };

        var results = new List<InstalledAppEntry>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (root, source) in _roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", enumerationOptions);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_extensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                var displayName = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                if (!IsMatch(Normalize(displayName), normalised))
                {
                    continue;
                }

                if (!seenPaths.Add(file))
                {
                    continue;
                }

                results.Add(new InstalledAppEntry(displayName, file, source));
                if (results.Count >= _maxMatches)
                {
                    return Task.FromResult<IReadOnlyList<InstalledAppEntry>>(results);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<InstalledAppEntry>>(results);
    }

    /// <summary>
    /// Returns true when <paramref name="queryNorm"/> is either a substring
    /// of <paramref name="nameNorm"/> or appears as an ordered subsequence
    /// of it. The subsequence fallback lets <c>"vscode"</c> match
    /// <c>"visualstudiocode"</c> (Visual Studio Code), while still rejecting
    /// disordered queries.
    /// </summary>
    public static bool IsMatch(string nameNorm, string queryNorm)
    {
        if (string.IsNullOrEmpty(queryNorm))
        {
            return false;
        }
        if (nameNorm.Contains(queryNorm, StringComparison.Ordinal))
        {
            return true;
        }
        var needle = 0;
        for (var i = 0; i < nameNorm.Length && needle < queryNorm.Length; i++)
        {
            if (nameNorm[i] == queryNorm[needle])
            {
                needle++;
            }
        }
        return needle == queryNorm.Length;
    }

    /// <summary>Lower-case, letter/digit-only normalisation of an app name.</summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }
        return sb.ToString();
    }

    private static IReadOnlyList<(string Path, string Source)> DefaultRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var publicFolder = Environment.GetEnvironmentVariable("PUBLIC") ?? string.Empty;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        return new List<(string Path, string Source)>
        {
            (Path.Combine(userProfile, "Desktop"), "UserDesktop"),
            (Path.Combine(publicFolder, "Desktop"), "PublicDesktop"),
            (Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs"), "UserStartMenu"),
            (Path.Combine(programData, "Microsoft", "Windows", "Start Menu", "Programs"), "CommonStartMenu"),
        };
    }
}
