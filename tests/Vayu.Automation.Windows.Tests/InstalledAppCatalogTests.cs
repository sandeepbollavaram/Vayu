using Vayu.Automation.Windows;

namespace Vayu.Automation.Windows.Tests;

public class InstalledAppCatalogTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _desktopRoot;
    private readonly string _startMenuRoot;

    public InstalledAppCatalogTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"vayu-cat-{Guid.NewGuid():N}");
        _desktopRoot = Path.Combine(_tempRoot, "Desktop");
        _startMenuRoot = Path.Combine(_tempRoot, "StartMenu");
        Directory.CreateDirectory(_desktopRoot);
        Directory.CreateDirectory(_startMenuRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    private InstalledAppCatalog NewCatalog()
    {
        return new InstalledAppCatalog(
            roots: new List<(string Path, string Source)>
            {
                (_desktopRoot, "UserDesktop"),
                (_startMenuRoot, "UserStartMenu"),
            });
    }

    private static void CreateShortcut(string directory, string fileNameWithoutExt, string extension)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileNameWithoutExt + extension), "fake-shortcut-content");
    }

    [Fact]
    public async Task Search_FindsLnk_ByNormalisedName()
    {
        CreateShortcut(_desktopRoot, "Spotify", ".lnk");
        var catalog = NewCatalog();

        var matches = await catalog.SearchAsync("spotify");

        var only = Assert.Single(matches);
        Assert.Equal("Spotify", only.DisplayName);
        Assert.Equal("UserDesktop", only.Source);
        Assert.EndsWith("Spotify.lnk", only.ShortcutPath);
    }

    [Fact]
    public async Task Search_IsCaseAndPunctuationInsensitive()
    {
        CreateShortcut(_startMenuRoot, "Visual Studio Code", ".lnk");
        var catalog = NewCatalog();

        var matches = await catalog.SearchAsync("vscode");

        Assert.Single(matches);
        Assert.Equal("Visual Studio Code", matches[0].DisplayName);
    }

    [Fact]
    public async Task Search_FindsUrlShortcuts()
    {
        CreateShortcut(_desktopRoot, "ChatGPT", ".url");
        var catalog = NewCatalog();

        var matches = await catalog.SearchAsync("chatgpt");

        Assert.Single(matches);
        Assert.EndsWith("ChatGPT.url", matches[0].ShortcutPath);
    }

    [Fact]
    public async Task Search_IgnoresNonShortcutFiles()
    {
        File.WriteAllText(Path.Combine(_desktopRoot, "notes.txt"), "hi");
        File.WriteAllText(Path.Combine(_desktopRoot, "ignore.exe"), "exe");
        var catalog = NewCatalog();

        var matches = await catalog.SearchAsync("notes");

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Search_RecursesIntoStartMenuSubfolders()
    {
        var subFolder = Path.Combine(_startMenuRoot, "Spotify");
        Directory.CreateDirectory(subFolder);
        CreateShortcut(subFolder, "Spotify", ".lnk");
        var catalog = NewCatalog();

        var matches = await catalog.SearchAsync("spotify");

        Assert.Single(matches);
        Assert.Contains("Spotify", matches[0].ShortcutPath);
    }

    [Fact]
    public async Task Search_ReturnsMultiple_WhenSameAppInMultipleRoots()
    {
        CreateShortcut(_desktopRoot, "Spotify", ".lnk");
        CreateShortcut(Path.Combine(_startMenuRoot, "Spotify"), "Spotify", ".lnk");
        var catalog = NewCatalog();

        var matches = await catalog.SearchAsync("spotify");

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.Source == "UserDesktop");
        Assert.Contains(matches, m => m.Source == "UserStartMenu");
    }

    [Fact]
    public async Task Search_ReturnsEmpty_WhenNoMatch()
    {
        CreateShortcut(_desktopRoot, "Slack", ".lnk");
        var catalog = NewCatalog();

        var matches = await catalog.SearchAsync("definitely-not-installed");

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Search_HandlesMissingRootsGracefully()
    {
        var catalog = new InstalledAppCatalog(
            roots: new List<(string Path, string Source)>
            {
                (Path.Combine(_tempRoot, "does-not-exist"), "UserDesktop"),
                ("", "EmptyRoot"),
            });

        var matches = await catalog.SearchAsync("anything");

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Search_Throws_OnNullOrWhitespaceQuery()
    {
        var catalog = NewCatalog();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => catalog.SearchAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => catalog.SearchAsync(""));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => catalog.SearchAsync("   "));
    }

    [Fact]
    public void Normalize_StripsWhitespaceAndPunctuationAndLowercases()
    {
        Assert.Equal("vscode", InstalledAppCatalog.Normalize("VS Code"));
        Assert.Equal("visualstudiocode", InstalledAppCatalog.Normalize("Visual Studio Code"));
        Assert.Equal("spotify", InstalledAppCatalog.Normalize("  Spotify!  "));
        Assert.Equal("", InstalledAppCatalog.Normalize("   "));
    }
}
