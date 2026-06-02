namespace Vayu.Core.Setup;

/// <summary>
/// The local folders Vayu uses for its workspace, logs, cache, model files, and
/// generated assets. Chosen during first-run setup; all default to a safe
/// subtree under <c>%LOCALAPPDATA%\Vayu</c>. No path here is a secret.
/// </summary>
/// <param name="WorkspaceRoot">Where generated projects/files are saved by default.</param>
/// <param name="LogsPath">Where Vayu writes its logs.</param>
/// <param name="CachePath">Scratch/cache directory.</param>
/// <param name="ModelsPath">Local model files (e.g. Whisper GGUF).</param>
/// <param name="AssetsPath">Generated assets (images, renders, etc.).</param>
public sealed record VayuStoragePaths(
    string WorkspaceRoot,
    string LogsPath,
    string CachePath,
    string ModelsPath,
    string AssetsPath)
{
    /// <summary>
    /// The default layout under <c>%LOCALAPPDATA%\Vayu</c> (resolved at call time).
    /// </summary>
    public static VayuStoragePaths Default()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vayu");
        return new VayuStoragePaths(
            WorkspaceRoot: Path.Combine(root, "workspace"),
            LogsPath: Path.Combine(root, "logs"),
            CachePath: Path.Combine(root, "cache"),
            ModelsPath: Path.Combine(root, "models"),
            AssetsPath: Path.Combine(root, "assets"));
    }

    /// <summary>All five paths in a stable order, for iteration in the UI / creation.</summary>
    public IReadOnlyList<string> AllPaths =>
        [WorkspaceRoot, LogsPath, CachePath, ModelsPath, AssetsPath];

    /// <summary>
    /// True when a path is a syntactically valid, rooted (absolute) local path —
    /// the minimum bar before Vayu will offer to create it. Does not touch disk.
    /// </summary>
    public static bool IsValidPathShape(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        try
        {
            var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            return Path.IsPathRooted(full)
                && full.IndexOfAny(Path.GetInvalidPathChars()) < 0;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
