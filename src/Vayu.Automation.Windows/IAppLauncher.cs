using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// Launches a known application or known folder. M1 deliberately does
/// NOT support clicking, typing, screen reading, screenshots, or admin
/// elevation — only safe "open this thing" operations against entries
/// registered in <see cref="KnownAppCatalog"/>.
/// </summary>
/// <remarks>
/// Implementations MUST validate the <c>appId</c> against the catalog
/// and never accept an arbitrary file path. This prevents command-
/// injection-style escalations through user input.
/// </remarks>
public interface IAppLauncher
{
    /// <summary>
    /// Launches the catalog entry identified by <paramref name="appId"/>.
    /// Returns <see cref="CommandResult.Success"/> on launch or
    /// <see cref="CommandResult.Failed"/> with a stable error code
    /// (e.g. <c>UNKNOWN_APP</c>, <c>LAUNCH_FAILED</c>) on any failure.
    /// </summary>
    Task<CommandResult> LaunchAsync(string appId, CancellationToken cancellationToken = default);
}
