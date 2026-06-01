using System.Runtime.Versioning;

using Vayu.Automation.Windows;

namespace Vayu.Automation.Windows.Tests;

[SupportedOSPlatform("windows")]
public class WindowsAppPathsResolverTests : IDisposable
{
    private readonly string _tempExe;

    public WindowsAppPathsResolverTests()
    {
        // A real, existing .exe path on disk so the File.Exists branch is covered
        // without touching the registry.
        _tempExe = Path.Combine(Path.GetTempPath(), $"vayu-apppaths-{Guid.NewGuid():N}.exe");
        File.WriteAllText(_tempExe, "stub");
    }

    public void Dispose()
    {
        try { File.Delete(_tempExe); } catch { /* best-effort */ }
    }

    [Fact]
    public void IsSafeExecutablePath_AllowsBareExistingExe()
    {
        Assert.True(WindowsAppPathsResolver.IsSafeExecutablePath(_tempExe));
    }

    [Fact]
    public void IsSafeExecutablePath_AllowsSingleQuotedExistingExe()
    {
        Assert.True(WindowsAppPathsResolver.IsSafeExecutablePath($"\"{_tempExe}\""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSafeExecutablePath_RejectsNullOrBlank(string? value)
    {
        Assert.False(WindowsAppPathsResolver.IsSafeExecutablePath(value));
    }

    [Fact]
    public void IsSafeExecutablePath_RejectsNonExistentExe()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"vayu-missing-{Guid.NewGuid():N}.exe");
        Assert.False(WindowsAppPathsResolver.IsSafeExecutablePath(missing));
    }

    [Fact]
    public void IsSafeExecutablePath_RejectsNonExeExtension()
    {
        var txt = Path.ChangeExtension(_tempExe, ".txt");
        File.WriteAllText(txt, "stub");
        try
        {
            Assert.False(WindowsAppPathsResolver.IsSafeExecutablePath(txt));
        }
        finally
        {
            try { File.Delete(txt); } catch { /* best-effort */ }
        }
    }

    [Theory]
    // Command-line-shaped values must never be treated as a launchable path —
    // this is what blocks a poisoned App Paths value from smuggling arguments.
    [InlineData("\"C:\\app.exe\" --do-evil")]
    [InlineData("C:\\app.exe /c calc")]
    [InlineData("cmd.exe /c whoami")]
    [InlineData("\"C:\\a.exe\" \"C:\\b.exe\"")]
    public void IsSafeExecutablePath_RejectsCommandLines(string value)
    {
        Assert.False(WindowsAppPathsResolver.IsSafeExecutablePath(value));
    }

    [Fact]
    public void TryResolveExecutable_UnknownAppId_ReturnsNull()
    {
        var resolver = new WindowsAppPathsResolver();

        // Not in the candidate map → no registry read, null result.
        Assert.Null(resolver.TryResolveExecutable("definitely-not-a-real-app"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolveExecutable_Throws_OnNullOrBlank(string? appId)
    {
        var resolver = new WindowsAppPathsResolver();

        Assert.ThrowsAny<ArgumentException>(() => resolver.TryResolveExecutable(appId!));
    }
}
