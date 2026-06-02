namespace Vayu.Core.Setup;

/// <summary>
/// Exposes the <b>active</b> Vayu storage paths to runtime services. These come
/// from the user's first-run setup choice when valid, otherwise the safe
/// defaults under <c>%LOCALAPPDATA%\Vayu</c>. Never exposes secrets.
/// </summary>
public interface IVayuStoragePathProvider
{
    /// <summary>The active storage paths runtime services should use.</summary>
    VayuStoragePaths ActivePaths { get; }

    /// <summary>True when <see cref="ActivePaths"/> came from the user's saved setup (not the default fallback).</summary>
    bool UsingCustomPaths { get; }
}

/// <summary>
/// Resolves and exposes the active <see cref="VayuStoragePaths"/>, creating the
/// directories if needed. Pure and deterministic: the persisted choice is passed
/// in (the desktop app loads it from the bootstrap settings store), so this type
/// has no storage dependency and is fully testable.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bootstrap note.</b> The setup-state database itself stays at the fixed
/// default location — it is what tells Vayu where the chosen root is, so it
/// cannot live under a path read from inside it. This provider therefore governs
/// logs, cache, models, and generated assets (and exposes the chosen root) but
/// not the bootstrap settings DB.
/// </para>
/// <para>
/// Existing data is never moved: choosing a new root takes effect going forward.
/// </para>
/// </remarks>
public sealed class VayuStoragePathProvider : IVayuStoragePathProvider
{
    /// <inheritdoc />
    public VayuStoragePaths ActivePaths { get; }

    /// <inheritdoc />
    public bool UsingCustomPaths { get; }

    /// <param name="persisted">
    /// The storage paths saved during setup, or <see langword="null"/> when none
    /// were configured. Invalid entries are ignored in favour of the defaults.
    /// </param>
    /// <param name="createDirectories">
    /// When true (default), the active directories are created if missing.
    /// </param>
    public VayuStoragePathProvider(VayuStoragePaths? persisted, bool createDirectories = true)
    {
        if (persisted is not null && IsUsable(persisted))
        {
            ActivePaths = persisted;
            UsingCustomPaths = true;
        }
        else
        {
            ActivePaths = VayuStoragePaths.Default();
            UsingCustomPaths = false;
        }

        if (createDirectories)
        {
            EnsureDirectories(ActivePaths);
        }
    }

    /// <summary>All five active paths must be valid absolute paths to be usable.</summary>
    private static bool IsUsable(VayuStoragePaths paths)
        => paths.AllPaths.All(VayuStoragePaths.IsValidPathShape);

    /// <summary>
    /// Best-effort creation of the active directories. A failure here must not
    /// crash startup — services still fall back to their own defaults if a path
    /// is unwritable.
    /// </summary>
    public static void EnsureDirectories(VayuStoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (var p in paths.AllPaths)
        {
            try
            {
                Directory.CreateDirectory(p);
            }
#pragma warning disable CA1031 // Directory creation is best-effort; never crash startup.
            catch (Exception)
            {
                // Leave it; the consuming service handles an unwritable path.
            }
#pragma warning restore CA1031
        }
    }
}
