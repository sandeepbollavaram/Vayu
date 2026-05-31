using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// Launches a known application or a discovered shortcut. M1 deliberately
/// does NOT support clicking, typing, screen reading, screenshots, or
/// admin elevation — only safe "open this thing" operations against
/// entries registered in <see cref="KnownAppCatalog"/> or discovered by
/// <see cref="InstalledAppCatalog"/>.
/// </summary>
/// <remarks>
/// Implementations MUST validate the <c>appId</c> against the catalog and
/// MUST NOT accept an arbitrary file path through <see cref="LaunchAsync"/>.
/// Shortcut launches go through <see cref="LaunchShortcutAsync"/> with a
/// catalog-issued <see cref="InstalledAppEntry"/> only.
/// </remarks>
public interface IAppLauncher
{
    /// <summary>
    /// Launches the entry in <see cref="KnownAppCatalog"/> identified by
    /// <paramref name="appId"/>. Returns <see cref="CommandResult.Success"/>
    /// on launch or <see cref="CommandResult.Failed"/> with a stable error
    /// code (e.g. <c>UNKNOWN_APP</c>, <c>LAUNCH_FAILED</c>).
    /// </summary>
    Task<CommandResult> LaunchAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches a shortcut discovered by <see cref="InstalledAppCatalog"/>.
    /// The <paramref name="entry"/> must come from the catalog — callers
    /// must never synthesise one from arbitrary user text.
    /// </summary>
    Task<CommandResult> LaunchShortcutAsync(InstalledAppEntry entry, CancellationToken cancellationToken = default);
}
