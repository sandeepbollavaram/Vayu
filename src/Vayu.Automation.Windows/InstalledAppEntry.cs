namespace Vayu.Automation.Windows;

/// <summary>
/// A safe-to-launch entry discovered by <see cref="InstalledAppCatalog"/> —
/// a shortcut found on the user's Desktop or Start Menu. Vayu only launches
/// entries of this shape (or items from <see cref="KnownAppCatalog"/>); it
/// never accepts an arbitrary executable path from user text.
/// </summary>
/// <param name="DisplayName">The visible label, e.g. <c>"Spotify"</c>. Derived from the shortcut file name.</param>
/// <param name="ShortcutPath">Absolute path to the <c>.lnk</c> or <c>.url</c> file. Used as <c>FileName</c> on shell-execute.</param>
/// <param name="Source">Which scan root produced this entry (<c>"UserDesktop"</c>, <c>"PublicDesktop"</c>, <c>"UserStartMenu"</c>, <c>"CommonStartMenu"</c>).</param>
public sealed record InstalledAppEntry(
    string DisplayName,
    string ShortcutPath,
    string Source);
