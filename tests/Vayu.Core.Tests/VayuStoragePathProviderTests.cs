using Vayu.Core.Setup;

namespace Vayu.Core.Tests;

public class VayuStoragePathProviderTests : IDisposable
{
    private readonly string _tempRoot;

    public VayuStoragePathProviderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"vayu-paths-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    private VayuStoragePaths UnderTemp() => new(
        WorkspaceRoot: Path.Combine(_tempRoot, "workspace"),
        LogsPath: Path.Combine(_tempRoot, "logs"),
        CachePath: Path.Combine(_tempRoot, "cache"),
        ModelsPath: Path.Combine(_tempRoot, "models"),
        AssetsPath: Path.Combine(_tempRoot, "assets"));

    [Fact]
    public void NullPersisted_UsesDefault_NotCustom()
    {
        var provider = new VayuStoragePathProvider(persisted: null, createDirectories: false);

        Assert.False(provider.UsingCustomPaths);
        Assert.Equal(VayuStoragePaths.Default().WorkspaceRoot, provider.ActivePaths.WorkspaceRoot);
    }

    [Fact]
    public void ValidPersisted_UsesCustomPaths()
    {
        var custom = UnderTemp();

        var provider = new VayuStoragePathProvider(custom, createDirectories: false);

        Assert.True(provider.UsingCustomPaths);
        Assert.Equal(custom.LogsPath, provider.ActivePaths.LogsPath);
        Assert.Equal(custom.ModelsPath, provider.ActivePaths.ModelsPath);
    }

    [Fact]
    public void InvalidPersisted_FallsBackToDefault()
    {
        // A relative (non-rooted) path is invalid → fall back to defaults.
        var invalid = new VayuStoragePaths(
            WorkspaceRoot: "relative\\workspace",
            LogsPath: "relative\\logs",
            CachePath: "relative\\cache",
            ModelsPath: "relative\\models",
            AssetsPath: "relative\\assets");

        var provider = new VayuStoragePathProvider(invalid, createDirectories: false);

        Assert.False(provider.UsingCustomPaths);
        Assert.Equal(VayuStoragePaths.Default().LogsPath, provider.ActivePaths.LogsPath);
    }

    [Fact]
    public void CreateDirectories_MakesAllActiveFolders()
    {
        var custom = UnderTemp();

        _ = new VayuStoragePathProvider(custom, createDirectories: true);

        Assert.True(Directory.Exists(custom.WorkspaceRoot));
        Assert.True(Directory.Exists(custom.LogsPath));
        Assert.True(Directory.Exists(custom.CachePath));
        Assert.True(Directory.Exists(custom.ModelsPath));
        Assert.True(Directory.Exists(custom.AssetsPath));
    }

    [Fact]
    public void CustomChildPaths_AreDerivedFromRoot()
    {
        var custom = UnderTemp();

        var provider = new VayuStoragePathProvider(custom, createDirectories: false);

        foreach (var p in provider.ActivePaths.AllPaths)
        {
            Assert.StartsWith(_tempRoot, p);
        }
    }
}
