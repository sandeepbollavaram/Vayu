using Vayu.Core.Setup;

namespace Vayu.Core.Tests;

public class VayuStoragePathsTests
{
    [Fact]
    public void Default_PutsAllPathsUnderVayuRoot()
    {
        var paths = VayuStoragePaths.Default();

        Assert.Contains("Vayu", paths.WorkspaceRoot);
        Assert.EndsWith("workspace", paths.WorkspaceRoot);
        Assert.EndsWith("logs", paths.LogsPath);
        Assert.EndsWith("cache", paths.CachePath);
        Assert.EndsWith("models", paths.ModelsPath);
        Assert.EndsWith("assets", paths.AssetsPath);
        Assert.Equal(5, paths.AllPaths.Count);
    }

    [Theory]
    [InlineData(@"C:\Vayu")]
    [InlineData(@"D:\data\vayu workspace")]
    public void IsValidPathShape_AcceptsAbsolutePaths(string path)
    {
        Assert.True(VayuStoragePaths.IsValidPathShape(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative\\path")]   // not rooted
    [InlineData("workspace")]        // not rooted
    public void IsValidPathShape_RejectsInvalidOrRelative(string? path)
    {
        Assert.False(VayuStoragePaths.IsValidPathShape(path));
    }

    [Fact]
    public void IsValidPathShape_ExpandsEnvironmentVariables()
    {
        Assert.True(VayuStoragePaths.IsValidPathShape(@"%LOCALAPPDATA%\Vayu"));
    }
}
